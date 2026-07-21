namespace Pages.Reporting.Core.Model;

/// <summary>
/// The root of a report definition. Serializable to a single self-contained JSON document
/// via <see cref="Serialization.ReportJson"/>; the library never persists it — the caller does.
/// </summary>
public sealed class Report
{
    public int SchemaVersion { get; set; } = 1;

    public required string Name { get; set; }

    public List<ConnectionDefinition> Connections { get; set; } = [];

    /// <summary>Named shared data sources; each runs once per generation.</summary>
    public List<DataSetDefinition> DataSets { get; set; } = [];

    /// <summary>Report parameters usable in data set SQL as @name; hosts override defaults at generation time.</summary>
    public List<ParameterDefinition> Parameters { get; set; } = [];

    public List<ReportElement> Elements { get; set; } = [];

    public PageSettings Page { get; set; } = new();

    /// <summary>
    /// Gap between components on the layout grid — CSS gap shorthand "row column"
    /// (bare numbers = px). Null = default "12 16"; "0" makes boxes touch
    /// (use per-side borders to avoid doubled lines at shared edges).
    /// </summary>
    public string? Spacing { get; set; }

    /// <summary>Page-level styling (padding, border, background, base text style).</summary>
    public ElementStyle? Style { get; set; }
}

/// <summary>Print/PDF page settings; ignored for on-screen rendering.</summary>
public sealed class PageSettings
{
    public string Size { get; set; } = "A4";
    public bool Landscape { get; set; }
    public string Margin { get; set; } = "15mm";

    /// <summary>
    /// Print a footer (report name + page numbers) on each PDF page. Off by default:
    /// the footer needs extra bottom margin, making the printable area shorter than
    /// the on-screen page.
    /// </summary>
    public bool ShowFooter { get; set; }

    /// <summary>Extra bottom margin (mm) the PDF reserves when <see cref="ShowFooter"/> is on.</summary>
    public const double FooterReserveMm = 8;
}
