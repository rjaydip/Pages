namespace Pages.Reporting.Core.Model;

/// <summary>A static report title with an optional subtitle. No data binding.</summary>
public sealed class TitleElement : ReportElement
{
    public required string Text { get; set; }

    public string? Subtitle { get; set; }
}
