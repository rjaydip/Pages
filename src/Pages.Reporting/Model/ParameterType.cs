namespace Pages.Reporting.Core.Model;

/// <summary>
/// The kind of value a report parameter holds. Drives the input control
/// <c>&lt;ReportParameters&gt;</c> renders and how the value is bound to a data set's SQL.
/// Parsing is InvariantCulture / ISO-8601 — a report-level culture is a later feature.
/// </summary>
public enum ParameterType
{
    /// <summary>A free string (the default). Bound to SQL as text unless it reads exactly as a number.</summary>
    Text = 0,

    /// <summary>A number. Bound to SQL as an integer or a decimal; overrides the "is this text or a number?" heuristic.</summary>
    Number = 1,

    /// <summary>A calendar date (<c>yyyy-MM-dd</c>, optionally with a time).</summary>
    Date = 2,

    /// <summary>A yes/no value — <c>true</c> or <c>false</c>. Bound to SQL as a boolean.</summary>
    Boolean = 3,

    /// <summary>A choice from <see cref="ParameterDefinition.AllowedValues"/>.</summary>
    List = 4,
}
