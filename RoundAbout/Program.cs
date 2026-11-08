using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;

// Round About: round every payment up to the next whole OMR and save the difference.
record PaymentRecord(DateTime Date, string Shop, decimal Amount, decimal Charged, decimal Saved);

class AppData
{
    public decimal Balance { get; set; } = 1000m;
    public decimal Savings { get; set; } = 0m;
    public decimal Goal { get; set; } = 0m;
    public List<PaymentRecord> Payments { get; set; } = new();
}

class Program
{
    const string SaveFile = "roundabout.json";
    static AppData data = new();
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    static void Main()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Load();

        while (true)
        {
            ShowMenu();
            string choice = (Console.ReadLine() ?? "").Trim().ToLowerInvariant();

            switch (choice)
            {
                case "1": case "pay": Pay(); break;
                case "2": case "history": History(); break;
                case "3": case "balance": Balance(); break;
                case "4": case "savings": Savings(); break;
                case "5": case "summary": Summary(); break;
                case "6": case "deposit": Deposit(); break;
                case "7": case "goal": SetGoal(); break;
                case "8": case "undo": Undo(); break;
                case "0": case "exit":
                    Print("\nGoodbye! Keep saving.", ConsoleColor.Cyan);
                    return;
                default:
                    Print("\n[Warning] Unknown option. Please try again.", ConsoleColor.Yellow);
                    break;
            }
        }
    }

    // ---------- Menu ----------

    static void ShowMenu()
    {
        Console.WriteLine();
        Print("╔════════════════════════════════╗", ConsoleColor.Cyan);
        Print("║        ROUND ABOUT  (OMR)      ║", ConsoleColor.Cyan);
        Print("╚════════════════════════════════╝", ConsoleColor.Cyan);
        Console.WriteLine($"  Balance: {Money(data.Balance)}   Saved: {Money(data.Savings)}");
        Console.WriteLine();
        Console.WriteLine("  1. Pay            5. Summary");
        Console.WriteLine("  2. History        6. Deposit");
        Console.WriteLine("  3. Balance        7. Savings goal");
        Console.WriteLine("  4. Savings        8. Undo last payment");
        Console.WriteLine("  0. Exit");
        Console.Write("\nEnter choice: ");
    }

    // ---------- Features ----------

    static void Pay()
    {
        Console.WriteLine("\nEnter amount in OMR and shop name (e.g. 1.2, Target):");
        string[] parts = (Console.ReadLine() ?? "").Split(',', 2);

        if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[1]))
        {
            Print("[Error] Invalid format. Use 'Amount, Shop Name'.", ConsoleColor.Red);
            return;
        }

        if (!TryParseAmount(parts[0], out decimal amount))
        {
            Print("[Error] Amount must be a positive number.", ConsoleColor.Red);
            return;
        }

        string shop = parts[1].Trim();
        decimal charged = Math.Ceiling(amount);
        decimal saved = charged - amount;

        if (charged > data.Balance)
        {
            Print($"[Error] Insufficient balance. You need {Money(charged)} but have {Money(data.Balance)}.", ConsoleColor.Red);
            return;
        }

        data.Balance -= charged;
        data.Savings += saved;
        data.Payments.Add(new PaymentRecord(DateTime.Now, shop, amount, charged, saved));
        Save();

        Print($"\n[Success] Paid {Money(amount)} at {shop}", ConsoleColor.Green);
        Console.WriteLine($"  Rounded up to : {Money(charged)}");
        Console.WriteLine($"  Saved         : {Money(saved)}");
        Console.WriteLine($"  New balance   : {Money(data.Balance)}");
        Console.WriteLine($"  Total savings : {Money(data.Savings)}");
        ShowGoalProgress();
    }

    static void History()
    {
        if (data.Payments.Count == 0)
        {
            Print("\nNo payments yet.", ConsoleColor.Yellow);
            return;
        }

        Console.Write("\nFilter by shop name (or press Enter to show all): ");
        string filter = (Console.ReadLine() ?? "").Trim();

        var list = data.Payments
            .Select((p, i) => (p, n: i + 1))
            .Where(x => filter.Length == 0 || x.p.Shop.Contains(filter, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (list.Count == 0)
        {
            Print($"No payments found for '{filter}'.", ConsoleColor.Yellow);
            return;
        }

        Print($"\n{"#",3}  {"Date",-13} {"Shop",-18} {"Amount",9} {"Charged",9} {"Saved",8}", ConsoleColor.Cyan);
        Console.WriteLine(new string('─', 66));
        foreach (var (p, n) in list)
        {
            string shop = p.Shop.Length > 18 ? p.Shop[..17] + "…" : p.Shop;
            Console.WriteLine($"{n,3}  {p.Date:dd MMM HH:mm} {shop,-18} {p.Amount,9:0.000} {p.Charged,9:0.000} {p.Saved,8:0.000}");
        }
        Console.WriteLine(new string('─', 66));
        Console.WriteLine($"Showing {list.Count} payment(s). Total saved: {Money(list.Sum(x => x.p.Saved))}");
    }

    static void Balance()
    {
        Print($"\nYour current balance is: {Money(data.Balance)}", ConsoleColor.Green);
    }

    static void Savings()
    {
        Print($"\nYour total savings is: {Money(data.Savings)}", ConsoleColor.Green);
        ShowGoalProgress();
    }

    static void Summary()
    {
        if (data.Payments.Count == 0)
        {
            Print("\nNo payments yet, nothing to summarise.", ConsoleColor.Yellow);
            return;
        }

        var top = data.Payments
            .GroupBy(p => p.Shop, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(g => g.Sum(p => p.Amount))
            .First();

        Print("\n── Summary ──", ConsoleColor.Cyan);
        Console.WriteLine($"Payments made    : {data.Payments.Count}");
        Console.WriteLine($"Total spent      : {Money(data.Payments.Sum(p => p.Amount))}");
        Console.WriteLine($"Average payment  : {Money(data.Payments.Average(p => p.Amount))}");
        Console.WriteLine($"Largest payment  : {Money(data.Payments.Max(p => p.Amount))}");
        Console.WriteLine($"Top shop         : {top.Key} ({Money(top.Sum(p => p.Amount))})");
        Console.WriteLine($"Total saved      : {Money(data.Savings)}");
        ShowGoalProgress();
    }

    static void Deposit()
    {
        Console.Write("\nAmount to deposit (OMR): ");
        if (!TryParseAmount(Console.ReadLine() ?? "", out decimal amount))
        {
            Print("[Error] Amount must be a positive number.", ConsoleColor.Red);
            return;
        }

        data.Balance += amount;
        Save();
        Print($"[Success] Deposited {Money(amount)}. New balance: {Money(data.Balance)}", ConsoleColor.Green);
    }

    static void SetGoal()
    {
        Console.Write("\nSet a savings goal in OMR (0 to clear): ");
        string input = (Console.ReadLine() ?? "").Trim();

        if (!decimal.TryParse(input, NumberStyles.Number, Inv, out decimal goal) || goal < 0 || goal > 1_000_000m)
        {
            Print("[Error] Please enter a valid number (0 or more).", ConsoleColor.Red);
            return;
        }

        data.Goal = goal;
        Save();
        Print(goal == 0 ? "Goal cleared." : $"Goal set to {Money(goal)}.", ConsoleColor.Green);
        ShowGoalProgress();
    }

    static void Undo()
    {
        if (data.Payments.Count == 0)
        {
            Print("\nNo payments to undo.", ConsoleColor.Yellow);
            return;
        }

        var last = data.Payments[^1];
        Console.Write($"\nUndo {Money(last.Amount)} at {last.Shop}? (y/n): ");
        if ((Console.ReadLine() ?? "").Trim().ToLowerInvariant() != "y")
        {
            Console.WriteLine("Cancelled.");
            return;
        }

        data.Payments.RemoveAt(data.Payments.Count - 1);
        data.Balance += last.Charged;
        data.Savings -= last.Saved;
        Save();
        Print("[Success] Last payment undone and refunded.", ConsoleColor.Green);
    }

    // ---------- Helpers ----------

    static void ShowGoalProgress()
    {
        if (data.Goal <= 0) return;

        decimal ratio = Math.Min(data.Savings / data.Goal, 1m);
        int filled = (int)(ratio * 20);
        string bar = new string('█', filled) + new string('░', 20 - filled);
        Console.WriteLine($"  Goal: [{bar}] {ratio * 100:0}% of {Money(data.Goal)}");
        if (data.Savings >= data.Goal)
            Print("  🎉 Goal reached!", ConsoleColor.Yellow);
    }

    static bool TryParseAmount(string text, out decimal amount)
    {
        return decimal.TryParse(text.Trim(), NumberStyles.Number, Inv, out amount)
               && amount > 0 && amount <= 1_000_000m;
    }

    static string Money(decimal value) => value.ToString("0.000", Inv) + " OMR";

    static void Print(string text, ConsoleColor color)
    {
        Console.ForegroundColor = color;
        Console.WriteLine(text);
        Console.ResetColor();
    }

    static void Save()
    {
        try
        {
            File.WriteAllText(SaveFile, JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
        {
            Print($"[Warning] Could not save data: {ex.Message}", ConsoleColor.Yellow);
        }
    }

    static void Load()
    {
        try
        {
            if (File.Exists(SaveFile))
                data = JsonSerializer.Deserialize<AppData>(File.ReadAllText(SaveFile)) ?? new AppData();
        }
        catch
        {
            Print("[Warning] Save file unreadable, starting fresh.", ConsoleColor.Yellow);
            data = new AppData();
        }
    }
}