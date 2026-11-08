using System;
using System.Globalization;

public static class RoundAboutLogic
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static decimal RoundUp(decimal amount) => Math.Ceiling(amount);

    public static decimal SavedAmount(decimal amount, decimal charged) => charged - amount;

    public static bool TryParseAmount(string text, out decimal amount)
    {
        return decimal.TryParse(text.Trim(), NumberStyles.Number, Inv, out amount)
               && amount > 0 && amount <= 1_000_000m;
    }

    public static string Money(decimal value) => value.ToString("0.000", Inv) + " OMR";

    public static (decimal Charged, decimal Saved) ProcessPayment(decimal amount)
    {
        decimal charged = RoundUp(amount);
        return (charged, SavedAmount(amount, charged));
    }
}
