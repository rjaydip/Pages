using Pages.Reporting.Core.Data;

namespace Pages.Reporting.Core.Model;

/// <summary>A headline metric tile. Place several inside a row section to build a KPI strip.</summary>
public sealed class KpiCardElement : ReportElement
{
    public required string Label { get; set; }

    public required DataBinding Binding { get; set; }

    public string? Format { get; set; }

    /// <summary>Optional caption shown under the value, e.g. "vs last month".</summary>
    public string? Caption { get; set; }

    /// <summary>Optional accent color (any CSS color) for the card's edge.</summary>
    public string? Accent { get; set; }
}
