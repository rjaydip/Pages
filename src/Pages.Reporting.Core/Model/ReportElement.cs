using System.Text.Json.Serialization;

namespace Pages.Reporting.Core.Model;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(TextElement), "text")]
[JsonDerivedType(typeof(TitleElement), "title")]
[JsonDerivedType(typeof(ValueElement), "value")]
[JsonDerivedType(typeof(KpiCardElement), "kpiCard")]
[JsonDerivedType(typeof(TableElement), "table")]
[JsonDerivedType(typeof(ChartElement), "chart")]
[JsonDerivedType(typeof(SectionElement), "section")]
public abstract class ReportElement
{
    public string? Id { get; set; }

    /// <summary>
    /// Optional CSS class(es) the developer wants on this element's root markup.
    /// The library ships no styling of its own — this plus the stable pr-* class
    /// names are the hooks a consuming app styles against.
    /// </summary>
    public string? CssClass { get; set; }

    /// <summary>Optional built-in styling (bold, size, align, padding, border, …).</summary>
    public ElementStyle? Style { get; set; }

    /// <summary>Width on the 12-column page grid (1–12). Null = full width.</summary>
    public int? ColumnSpan { get; set; }

    /// <summary>
    /// Minimum height of the element's grid cell (CSS length; bare number = px).
    /// Content may still grow taller; in a side-by-side row the tallest value
    /// sets the row height. Null = auto. (Charts size their drawing via their own
    /// Height property — this is the layout cell.)
    /// </summary>
    public string? MinHeight { get; set; }

    /// <summary>
    /// Grid rows this element spans (null = 1). Lets a tall element sit beside a
    /// vertical stack: order elements A (rowSpan 2), B1, C (rowSpan 2), B2 — items
    /// fill free slots left to right, so B2 lands under B1.
    /// </summary>
    public int? RowSpan { get; set; }
}
