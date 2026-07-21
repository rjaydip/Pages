using Pages.Reporting.Core.Data;

namespace Pages.Reporting.Core.Model;

/// <summary>A single labeled value, typically a scalar fetched from the database.</summary>
public sealed class ValueElement : ReportElement
{
    public required string Label { get; set; }

    public required DataBinding Binding { get; set; }

    /// <summary>.NET format string applied to the value, e.g. "N2", "C", "P0".</summary>
    public string? Format { get; set; }
}
