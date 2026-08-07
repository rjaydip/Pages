using System.Globalization;

namespace Pages.Reporting.Core.Rendering;

/// <summary>Shared value formatting so screen, PDF, and Excel show identical text.</summary>
public static class ValueFormatter
{
    public static string Format(object? value, string? format, CultureInfo? culture = null)
    {
        culture ??= CultureInfo.CurrentCulture;
        return value switch
        {
            null => string.Empty,
            IFormattable formattable when !string.IsNullOrEmpty(format) => TryFormat(formattable, format, culture),
            IFormattable formattable => formattable.ToString(null, culture),
            _ => value.ToString() ?? string.Empty,
        };
    }

    // The caller owns the report JSON, so `format` is untrusted. An invalid standard specifier
    // ("Q", or "D" on a double) throws, and inside a group band that happens mid-render where
    // nothing catches it — taking the whole page, the PDF and the spreadsheet with it. Falling
    // back to the default format shows the value rather than losing the report.
    private static string TryFormat(IFormattable value, string format, CultureInfo culture)
    {
        try
        {
            return value.ToString(format, culture);
        }
        catch (FormatException)
        {
            return value.ToString(null, culture);
        }
    }

    public static bool IsNumeric(object? value) => value is byte or sbyte or short or ushort or int or uint
        or long or ulong or float or double or decimal;

    public static double ToDouble(object? value) => value switch
    {
        null => 0,
        double d => d,
        IConvertible convertible => convertible.ToDouble(CultureInfo.InvariantCulture),
        _ => 0,
    };
}
