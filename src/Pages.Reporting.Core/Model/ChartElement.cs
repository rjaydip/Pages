using Pages.Reporting.Core.Data;

namespace Pages.Reporting.Core.Model;

/// <summary>A chart bound to a row-producing data source, rendered as SVG on screen and in PDF.</summary>
public sealed class ChartElement : ReportElement
{
    public string? Title { get; set; }

    public ChartType ChartType { get; set; } = ChartType.Bar;

    public required DataBinding Binding { get; set; }

    /// <summary>Field providing category labels (x axis / pie slices).</summary>
    public required string LabelField { get; set; }

    /// <summary>Fields providing the numeric series. Pie charts use the first one.</summary>
    public List<string> ValueFields { get; set; } = [];

    /// <summary>Format string for values shown in axis labels / legends.</summary>
    public string? Format { get; set; }

    /// <summary>Rendered height in pixels.</summary>
    public int Height { get; set; } = 320;
}

public enum ChartType
{
    Bar,
    Line,
    Pie,
}
