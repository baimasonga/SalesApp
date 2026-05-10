using System.Globalization;

namespace SalesApp.Services;

public static class Money
{
    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

    public static string Format(decimal amount, string currency = "NLe") =>
        $"{currency} {amount.ToString("N2", Culture)}";

    public static string FormatShort(decimal amount, string currency = "NLe")
    {
        if (amount >= 1_000_000) return $"{currency} {(amount / 1_000_000).ToString("N1", Culture)}M";
        if (amount >= 1_000) return $"{currency} {(amount / 1_000).ToString("N1", Culture)}K";
        return $"{currency} {amount.ToString("N0", Culture)}";
    }
}
