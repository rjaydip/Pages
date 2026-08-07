using System.Globalization;

namespace Pages.Reporting.Core.Model;

/// <summary>
/// Converts the CSS length strings the report model stores (the page margin above all) into
/// millimetres. The model keeps them as authored — "18mm", "2cm", "120" — so anything that
/// needs to compare or scale two of them has to agree on how they are read. Geometry itself
/// is already millimetres, so this is only needed at the authored-string edge.
/// </summary>
public static class CssLength
{
    /// <summary>
    /// Millimetres for a CSS length, or <paramref name="fallbackMm"/> when it is empty or
    /// unparseable. A bare number is CSS pixels, matching how the renderer emits it.
    /// </summary>
    public static double ToMm(string? value, double fallbackMm = 0)
    {
        if (string.IsNullOrWhiteSpace(value))
            return fallbackMm;

        var text = value.Trim().ToLowerInvariant();
        var (number, unit) = SplitUnit(text);
        if (!double.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
            return fallbackMm;

        return unit switch
        {
            "mm" => parsed,
            "cm" => parsed * 10,
            "in" => parsed * 25.4,
            "pt" => parsed * 25.4 / 72,
            "pc" => parsed * 25.4 / 6,
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
