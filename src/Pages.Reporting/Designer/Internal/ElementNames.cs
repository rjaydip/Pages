using Pages.Reporting.Core.Model;

namespace Pages.Reporting.Designer.Internal;

/// <summary>Display names for element types, shown on canvas badges and the properties head.</summary>
internal static class ElementNames
{
    public static string TypeName(ReportElement element) => element switch
    {
        TextElement => "Text",
        ChartElement chart => $"Chart · {chart.ChartType}",
        TableElement => "Table",
        SectionElement { Columns: { } columns } => $"Section · {columns}/row",
        SectionElement section => $"Section · {(section.Direction == SectionDirection.Row ? "row" : "column")}",
        _ => element.GetType().Name,
    };
}
