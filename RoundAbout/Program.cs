using System.Globalization;
using System.Text;
using System.Text.Json;

public enum RoundingOption
{
    HalfOmani,
    TenthOmani
}

public sealed class PaymentRecord
{
    public DateTime Date { get; set; }
    public string Shop { get; set; } = "";
    public decimal Amount { get; set; }
    public decimal Charged { get; set; }
    public decimal Saved { get; set; }
}

public sealed class Account
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Personal";
    public decimal Balance { get; set; } = 1000m;
    public decimal Savings { get; set; }
    public decimal Goal { get; set; }
    public List<PaymentRecord> Payments { get; set; } = new();
}

public sealed class AppData
{
    public List<Account> Accounts { get; set; } = new();
    public Guid? ActiveAccountId { get; set; }
    public RoundingOption Rounding { get; set; } = RoundingOption.TenthOmani;

    // Kept for loading files created by the original single-account version.
    public decimal Balance { get; set; }
    public decimal Savings { get; set; }
    public decimal Goal { get; set; }
    public List<PaymentRecord>? Payments { get; set; }

    public void Normalize()
    {
        if (Accounts.Count == 0)
        {
            Accounts.Add(new Account
            {
                Name = "Personal",
                Balance = Balance == 0 ? 1000m : Balance,
                Savings = Savings,
                Goal = Goal,
                Payments = Payments ?? new List<PaymentRecord>()
            });
        }

        if (ActiveAccountId is null || Accounts.All(a => a.Id != ActiveAccountId))
            ActiveAccountId = Accounts[0].Id;
    }

    public Account ActiveAccount
    {
        get
        {
            Normalize();
            return Accounts.Single(a => a.Id == ActiveAccountId);
        }
    }
}

public static class RoundAboutService
{
    public static decimal Increment(RoundingOption option) =>
        option == RoundingOption.HalfOmani ? 0.5m : 0.1m;

    public static decimal CalculateCharge(decimal amount, RoundingOption option)
    {
        if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));
        decimal increment = Increment(option);
        return Math.Ceiling(amount / increment) * increment;
    }

    public static PaymentRecord AddPayment(Account account, decimal amount, string shop, RoundingOption option, DateTime? date = null)
    {
        if (amount <= 0 || amount > 1_000_000m) throw new ArgumentOutOfRangeException(nameof(amount));
        if (string.IsNullOrWhiteSpace(shop)) throw new ArgumentException("Shop is required.", nameof(shop));

        decimal charged = CalculateCharge(amount, option);
        if (charged > account.Balance) throw new InvalidOperationException("Insufficient balance.");

        var payment = new PaymentRecord
        {
            Date = date ?? DateTime.Now,
            Shop = shop.Trim(),
            Amount = amount,
            Charged = charged,
            Saved = charged - amount
        };
        account.Balance -= charged;
        account.Savings += payment.Saved;
        account.Payments.Add(payment);
        return payment;
    }

    public static void UndoLastPayment(Account account)
    {
        if (account.Payments.Count == 0) throw new InvalidOperationException("No payments to undo.");
        PaymentRecord payment = account.Payments[^1];
        account.Payments.RemoveAt(account.Payments.Count - 1);
        account.Balance += payment.Charged;
        account.Savings -= payment.Saved;
    }

    public static string ExportCsv(IEnumerable<PaymentRecord> payments)
    {
        var builder = new StringBuilder("Date,Shop,Amount,Charged,Saved\r\n");
        foreach (PaymentRecord payment in payments)
        {
            builder.Append(Csv(payment.Date.ToString("O", CultureInfo.InvariantCulture))).Append(',')
                .Append(Csv(payment.Shop)).Append(',')
                .Append(payment.Amount.ToString("0.000", CultureInfo.InvariantCulture)).Append(',')
                .Append(payment.Charged.ToString("0.000", CultureInfo.InvariantCulture)).Append(',')
                .Append(payment.Saved.ToString("0.000", CultureInfo.InvariantCulture)).Append("\r\n");
        }
        return builder.ToString();
    }

    public static MonthlyReport GetMonthlyReport(Account account, int year, int month)
    {
        var payments = account.Payments.Where(p => p.Date.Year == year && p.Date.Month == month).ToList();
        return new MonthlyReport(year, month, payments.Count, payments.Sum(p => p.Amount), payments.Sum(p => p.Saved));
    }

    private static string Csv(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";
}

public sealed record MonthlyReport(int Year, int Month, int PaymentCount, decimal Spent, decimal Saved);

class Program
{
    private const string SaveFile = "roundabout.json";
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    private static AppData data = new();

    static void Main()
    {
        Console.OutputEncoding = Encoding.UTF8;
        Load();
        while (true)
        {
            ShowMenu();
            switch ((Console.ReadLine() ?? "").Trim().ToLowerInvariant())
            {
                case "1": case "pay": Pay(); break;
                case "2": case "history": History(); break;
                case "3": case "balance": Console.WriteLine($"\nYour current balance is: {Money(data.ActiveAccount.Balance)}"); break;
                case "4": case "savings": Savings(); break;
                case "5": case "summary": Summary(); break;
                case "6": case "deposit": Deposit(); break;
                case "7": case "goal": SetGoal(); break;
                case "8": case "undo": Undo(); break;
                case "9": case "accounts": Accounts(); break;
                case "10": case "rounding": ConfigureRounding(); break;
                case "11": case "export": ExportHistory(); break;
                case "12": case "monthly": MonthlyReport(); break;
                case "0": case "exit": Console.WriteLine("\nGoodbye! Keep saving."); return;
                default: Print("[Warning] Unknown option. Please try again.", ConsoleColor.Yellow); break;
            }
        }
    }

    static void ShowMenu()
    {
        Account account = data.ActiveAccount;
        Console.WriteLine($"\n╔════════════════════════════════╗\n║        ROUND ABOUT  (OMR)      ║\n╚════════════════════════════════╝");
        Console.WriteLine($"  Account: {account.Name}   Balance: {Money(account.Balance)}   Saved: {Money(account.Savings)}");
        Console.WriteLine($"  Rounding: {IncrementText(data.Rounding)}");
        Console.WriteLine("\n  1. Pay            7. Savings goal");
        Console.WriteLine("  2. History        8. Undo last payment");
        Console.WriteLine("  3. Balance        9. Accounts");
        Console.WriteLine("  4. Savings       10. Rounding");
        Console.WriteLine("  5. Summary       11. Export history CSV");
        Console.WriteLine("  6. Deposit       12. Monthly report");
        Console.Write("  0. Exit\n\nEnter choice: ");
    }

    static void Pay()
    {
        Console.Write("\nEnter amount in OMR and shop name (e.g. 1.2, Target): ");
        string[] parts = (Console.ReadLine() ?? "").Split(',', 2);
        if (parts.Length != 2 || !TryParseAmount(parts[0], out decimal amount) || string.IsNullOrWhiteSpace(parts[1]))
        {
            Print("[Error] Use 'Amount, Shop Name' with a positive amount.", ConsoleColor.Red); return;
        }
        try
        {
            PaymentRecord payment = RoundAboutService.AddPayment(data.ActiveAccount, amount, parts[1], data.Rounding);
            Save();
            Print($"\n[Success] Paid {Money(payment.Amount)} at {payment.Shop}", ConsoleColor.Green);
            Console.WriteLine($"  Charged: {Money(payment.Charged)}   Saved: {Money(payment.Saved)}");
        }
        catch (InvalidOperationException) { Print("[Error] Insufficient balance.", ConsoleColor.Red); }
    }

    static void History()
    {
        Account account = data.ActiveAccount;
        Console.Write("\nFilter by shop name (or press Enter for all): ");
        string filter = (Console.ReadLine() ?? "").Trim();
        var list = account.Payments.Where(p => filter.Length == 0 || p.Shop.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();
        if (list.Count == 0) { Print("\nNo payments found.", ConsoleColor.Yellow); return; }
        Console.WriteLine($"\n{"#",-3} {"Date",-16} {"Shop",-18} {"Amount",9} {"Charged",9} {"Saved",8}");
        foreach ((PaymentRecord p, int n) in list.Select((p, i) => (p, i + 1)))
            Console.WriteLine($"{n,-3} {p.Date:dd MMM yyyy HH:mm} {p.Shop[..Math.Min(18, p.Shop.Length)],-18} {p.Amount,9:0.000} {p.Charged,9:0.000} {p.Saved,8:0.000}");
        Console.WriteLine($"Showing {list.Count} payment(s). Total saved: {Money(list.Sum(p => p.Saved))}");
    }

    static void Savings() => Console.WriteLine($"\nYour total savings is: {Money(data.ActiveAccount.Savings)}");
    static void Summary()
    {
        Account account = data.ActiveAccount;
        if (account.Payments.Count == 0) { Print("\nNo payments yet.", ConsoleColor.Yellow); return; }
        var top = account.Payments.GroupBy(p => p.Shop, StringComparer.OrdinalIgnoreCase).OrderByDescending(g => g.Sum(p => p.Amount)).First();
        Console.WriteLine($"\n── Summary ({account.Name}) ──\nPayments made: {account.Payments.Count}\nTotal spent: {Money(account.Payments.Sum(p => p.Amount))}\nAverage payment: {Money(account.Payments.Average(p => p.Amount))}\nLargest payment: {Money(account.Payments.Max(p => p.Amount))}\nTop shop: {top.Key} ({Money(top.Sum(p => p.Amount))})\nTotal saved: {Money(account.Savings)}");
    }

    static void Deposit()
    {
        Console.Write("\nAmount to deposit (OMR): ");
        if (!TryParseAmount(Console.ReadLine() ?? "", out decimal amount)) { Print("[Error] Invalid amount.", ConsoleColor.Red); return; }
        data.ActiveAccount.Balance += amount; Save();
        Console.WriteLine($"Deposited {Money(amount)}. New balance: {Money(data.ActiveAccount.Balance)}");
    }

    static void SetGoal()
    {
        Console.Write("\nSet a savings goal in OMR (0 to clear): ");
        if (!decimal.TryParse(Console.ReadLine(), NumberStyles.Number, Inv, out decimal goal) || goal < 0 || goal > 1_000_000m)
        { Print("[Error] Please enter a valid number.", ConsoleColor.Red); return; }
        data.ActiveAccount.Goal = goal; Save();
        Console.WriteLine(goal == 0 ? "Goal cleared." : $"Goal set to {Money(goal)}.");
    }

    static void Undo()
    {
        try
        {
            PaymentRecord last = data.ActiveAccount.Payments[^1];
            Console.Write($"\nUndo {Money(last.Amount)} at {last.Shop}? (y/n): ");
            if ((Console.ReadLine() ?? "").Trim().ToLowerInvariant() != "y") return;
            RoundAboutService.UndoLastPayment(data.ActiveAccount); Save(); Console.WriteLine("Payment undone and refunded.");
        }
        catch (InvalidOperationException) { Print("\nNo payments to undo.", ConsoleColor.Yellow); }
    }

    static void Accounts()
    {
        Console.WriteLine("\nAccounts:");
        foreach (Account account in data.Accounts)
            Console.WriteLine($"  {(account.Id == data.ActiveAccountId ? "*" : " ")} {account.Name} ({Money(account.Balance)})");
        Console.Write("Enter account name to switch, or 'new <name>': ");
        string input = (Console.ReadLine() ?? "").Trim();
        if (input.StartsWith("new ", StringComparison.OrdinalIgnoreCase) && input[4..].Trim() is { Length: > 0 } name)
        {
            var account = new Account { Name = name };
            data.Accounts.Add(account); data.ActiveAccountId = account.Id; Save(); return;
        }
        Account? selected = data.Accounts.FirstOrDefault(a => a.Name.Equals(input, StringComparison.OrdinalIgnoreCase));
        if (selected is not null) { data.ActiveAccountId = selected.Id; Save(); }
    }

    static void ConfigureRounding()
    {
        Console.Write("\nChoose rounding: 1 = nearest 0.500 OMR, 2 = nearest 0.100 OMR: ");
        string choice = (Console.ReadLine() ?? "").Trim();
        if (choice == "1") data.Rounding = RoundingOption.HalfOmani;
        else if (choice == "2") data.Rounding = RoundingOption.TenthOmani;
        else { Print("[Error] Invalid rounding option.", ConsoleColor.Red); return; }
        Save(); Console.WriteLine($"Rounding set to {IncrementText(data.Rounding)}.");
    }

    static void ExportHistory()
    {
        string file = $"roundabout-{DateTime.Now:yyyyMMdd-HHmmss}.csv";
        File.WriteAllText(file, RoundAboutService.ExportCsv(data.ActiveAccount.Payments), Encoding.UTF8);
        Console.WriteLine($"History exported to {Path.GetFullPath(file)}");
    }

    static void MonthlyReport()
    {
        Console.Write("\nMonth (YYYY-MM, blank for current): ");
        string input = (Console.ReadLine() ?? "").Trim();
        DateTime month;
        if (input.Length == 0) month = DateTime.Now;
        else if (!DateTime.TryParseExact(input, "yyyy-MM", Inv, DateTimeStyles.None, out month))
        {
            Print("[Error] Enter a month in YYYY-MM format.", ConsoleColor.Red);
            return;
        }
        MonthlyReport report = RoundAboutService.GetMonthlyReport(data.ActiveAccount, month.Year, month.Month);
        Console.WriteLine($"\n{month:MMMM yyyy}: {report.PaymentCount} payment(s), spent {Money(report.Spent)}, saved {Money(report.Saved)}");
    }

    static bool TryParseAmount(string text, out decimal amount) =>
        decimal.TryParse(text.Trim(), NumberStyles.Number, Inv, out amount) && amount > 0 && amount <= 1_000_000m;
    static string Money(decimal value) => value.ToString("0.000", Inv) + " OMR";
    static string IncrementText(RoundingOption option) => RoundAboutService.Increment(option).ToString("0.000", Inv) + " OMR";
    static void Print(string text, ConsoleColor color) { Console.ForegroundColor = color; Console.WriteLine(text); Console.ResetColor(); }

    static void Save()
    {
        try { File.WriteAllText(SaveFile, JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true })); }
        catch (IOException ex) { Print($"[Warning] Could not save data: {ex.Message}", ConsoleColor.Yellow); }
    }

    static void Load()
    {
        try
        {
            if (File.Exists(SaveFile)) data = JsonSerializer.Deserialize<AppData>(File.ReadAllText(SaveFile)) ?? new AppData();
            data.Normalize();
        }
        catch (JsonException) { Print("[Warning] Save file unreadable, starting fresh.", ConsoleColor.Yellow); data = new AppData(); data.Normalize(); }
        catch (IOException ex) { Print($"[Warning] Could not load data: {ex.Message}", ConsoleColor.Yellow); data.Normalize(); }
    }
}
