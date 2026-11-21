using System.Globalization;

namespace PosApp.Services;

public static class Money
{
    public const string Symbol = "₱";

    public static long ToCents(decimal value) =>
        (long)Math.Round(value * 100m, MidpointRounding.AwayFromZero);

    public static decimal FromCents(long cents) => cents / 100m;

    public static string Format(decimal value) =>
        Symbol + value.ToString("N2", CultureInfo.InvariantCulture);

    /// <summary>Parses user input such as "45", "45.50" or "₱45.50".</summary>
    public static bool TryParse(string? text, out decimal value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(text)) return false;
        var cleaned = text.Replace(Symbol, "").Trim();
        return decimal.TryParse(cleaned, NumberStyles.Number, CultureInfo.CurrentCulture, out value)
            || decimal.TryParse(cleaned, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
    }
}
