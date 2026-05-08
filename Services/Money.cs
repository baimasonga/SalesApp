using System.Globalization;

namespace SalesApp.Services;

public static class Money
{
    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

    public static string Format(decimal amount) =>
        $"NLe {amount.ToString("N2", Culture)}";

    public static string FormatShort(decimal amount)
    {
        if (amount >= 1_000_000) return $"NLe {(amount / 1_000_000).ToString("N1", Culture)}M";
        if (amount >= 1_000) return $"NLe {(amount / 1_000).ToString("N1", Culture)}K";
        return $"NLe {amount.ToString("N0", Culture)}";
    }
}
