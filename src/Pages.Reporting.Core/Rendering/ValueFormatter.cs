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
            IFormattable formattable when !string.IsNullOrEmpty(format) => formattable.ToString(format, culture),
            IFormattable formattable => formattable.ToString(null, culture),
            _ => value.ToString() ?? string.Empty,
        };
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
