using System.Globalization;
using Pages.Reporting.Core.Model;

namespace Pages.Reporting.Core.Data;

/// <summary>
/// The outcome of parsing a raw parameter string for a declared <see cref="ParameterType"/>.
/// </summary>
/// <param name="Valid">
/// False when the string is not a valid value for the type. A null or empty input is
/// <em>valid</em> (it means "unset — use the default").
/// </param>
/// <param name="Canonical">
/// The normalised string (e.g. <c>"2026-01-09"</c> for a date, <c>"5000"</c> for a number),
/// or null when the input was empty or invalid.
/// </param>
public readonly record struct ParsedParameter(bool Valid, string? Canonical);

/// <summary>
/// Parses report-parameter strings for their declared type. InvariantCulture / ISO-8601 only —
/// locale-aware parsing is a later feature. This is the single place a typed value is
/// validated and normalised; the runtime pipeline stores the canonical form so <c>{@name}</c>
/// and SQL binding agree.
/// </summary>
public static class ParameterParsing
{
    private static readonly string[] DateFormats =
    [
        "yyyy-MM-dd",
        "yyyy-MM-ddTHH:mm",
        "yyyy-MM-ddTHH:mm:ss",
        "yyyy-MM-dd HH:mm:ss",
    ];

    /// <summary>Parses <paramref name="value"/> for <paramref name="type"/>.</summary>
    /// <param name="value">The raw string, as supplied or as the declared default.</param>
    /// <param name="type">The parameter's declared type.</param>
    /// <param name="allowedValues">The choices for a <see cref="ParameterType.List"/> parameter.</param>
    public static ParsedParameter TryParse(
        string? value, ParameterType type, IReadOnlyList<ParameterOption>? allowedValues = null)
    {
        if (string.IsNullOrEmpty(value))
            return new ParsedParameter(true, null);

        var text = type is ParameterType.Boolean ? value : value.Trim();
        if (text.Length == 0)
            return new ParsedParameter(true, null);

        return type switch
        {
            ParameterType.Number => ParseNumber(text),
            ParameterType.Date => ParseDate(text),
            ParameterType.Boolean => ParseBoolean(text),
            ParameterType.List => ParseList(text, allowedValues),
            _ => new ParsedParameter(true, null),   // Text — SQL binding keeps the raw string
        };
    }

    private static ParsedParameter ParseNumber(string text)
    {
        if (long.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var integer))
            return new ParsedParameter(true, integer.ToString(CultureInfo.InvariantCulture));

        if (decimal.TryParse(
                text,
                NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture,
                out var dec))
            return new ParsedParameter(true, dec.ToString(CultureInfo.InvariantCulture));

        return new ParsedParameter(false, null);
    }

    private static ParsedParameter ParseDate(string text)
    {
        if (!DateTime.TryParseExact(
                text, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            return new ParsedParameter(false, null);

        var canonical = date.TimeOfDay == TimeSpan.Zero
            ? date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            : date.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);
        return new ParsedParameter(true, canonical);
    }

    // Canonicalised to 1 / 0 so it binds cleanly to a bit / INTEGER / TINYINT column, which is
    // how SQL Server, SQLite and MySQL store a boolean. (A native PostgreSQL boolean column
    // wants true / false — compare with `@p = 1` there, or use a text parameter.)
    private static ParsedParameter ParseBoolean(string text)
    {
        var flag = text.ToLowerInvariant() switch
        {
            "true" or "1" => true,
            "false" or "0" => false,
            _ => (bool?)null,
        };
        return flag is { } value
            ? new ParsedParameter(true, value ? "1" : "0")
            : new ParsedParameter(false, null);
    }

    private static ParsedParameter ParseList(string text, IReadOnlyList<ParameterOption>? allowedValues)
    {
        if (allowedValues is null)
            return new ParsedParameter(false, null);

        foreach (var option in allowedValues)
            if (string.Equals(option.Value, text, StringComparison.Ordinal))
                return new ParsedParameter(true, option.Value);

        return new ParsedParameter(false, null);
    }
}
