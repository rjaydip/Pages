using System.Globalization;

namespace Pages.Reporting.Export;

/// <summary>Parses the page's print margin into millimetres for the PDF page setup.</summary>
internal static class PrintMargins
{
    public static double ComputeMm(string margin) => ToMm(margin, fallbackMm: 15);

    public static string Mm(double value) => FormattableString.Invariant($"{value:0.###}mm");

    private static double ToMm(string value, double fallbackMm)
    {
        value = value.Trim().ToLowerInvariant();
        var (number, unit) = SplitUnit(value);
        if (!double.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
            return fallbackMm;

        return unit switch
        {
            "mm" => parsed,
            "cm" => parsed * 10,
            "in" => parsed * 25.4,
            "pt" => parsed * 25.4 / 72,
            // bare numbers and px are CSS pixels (96/in)
            _ => parsed * 25.4 / 96,
        };
    }

    private static (string Number, string Unit) SplitUnit(string value)
    {
        var i = value.Length;
        while (i > 0 && !char.IsDigit(value[i - 1]) && value[i - 1] != '.')
            i--;
        return (value[..i], value[i..]);
    }
}
