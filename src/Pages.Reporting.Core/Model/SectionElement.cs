namespace Pages.Reporting.Core.Model;

/// <summary>
/// Layout container: stacks children vertically or lays them out side by side
/// (e.g. a row of KPI cards or two charts next to each other).
/// </summary>
public sealed class SectionElement : ReportElement
{
    public string? Title { get; set; }

    public SectionDirection Direction { get; set; } = SectionDirection.Column;

    /// <summary>
    /// Custom grid: how many children sit per row (1–12). When set it overrides
    /// <see cref="Direction"/>'s default layout; children wrap into rows automatically.
    /// </summary>
    public int? Columns { get; set; }

    public List<ReportElement> Children { get; set; } = [];
}

public enum SectionDirection
{
    Column,
    Row,
}
