using Pages.Reporting.Core.Model;
using Pages.Reporting.Core.Rendering;

namespace Pages.Reporting.Designer.Internal;

/// <summary>
/// The design-time stand-in for a real resolution. The designer never runs the report's
/// queries, so every element is "resolved" to what the author typed: a text element to its
/// own template, unsubstituted. Handing the render components authored strings — rather than
/// teaching them a design mode — is what keeps the canvas drawing through exactly the same
/// code path as the PDF, so the two cannot drift.
/// </summary>
internal static class DesignPreview
{
    public static ResolvedReport Resolve(Report report)
    {
        var data = new Dictionary<ReportElement, ResolvedData>();

        // Every null-guard in this file — here and in Flatten/StubTable below — exists for one
        // reason: the caller owns this JSON, ReportJson sets no RespectNullableAnnotations, and
        // none of Bands/Elements/Columns is `required`, so a `= []` initializer only ever runs
        // when the JSON key is ABSENT. An explicit "bands": null (or "elements": null, or a null
        // entry inside any of these lists — legal even where the element type isn't nullable)
        // deserializes straight through. Treat a null collection as empty and a null entry as
        // "skip" throughout, so no shape of caller-authored report can throw out of this file.
        var bands = report.Bands ?? [];
        foreach (var element in Flatten(bands
            .Where(band => band is not null)
            .SelectMany(band => band.Elements ?? (IEnumerable<ReportElement>)[])))
        {
            switch (element)
            {
                // ReportText reads Scalar when no GroupScope cascades, so the raw template
                // lands on the canvas unsubstituted — {SalesData.Region} stays legible,
                // which is the entire point of a design-time preview.
                case TextElement text:
                    data[element] = new ResolvedData { Scalar = text.Content };
                    break;
                case TableElement table:
                    data[element] = new ResolvedData { Table = StubTable(table) };
                    break;
            }
        }

        // Charts and sections get no entry at all. GetData falls back to ResolvedData.Empty,
        // which makes SvgChartRenderer draw its own "No data" placeholder at the chart's
        // configured height, and leaves a section to render its children normally.
        return new ResolvedReport(report, data);
    }

    /// <summary>
    /// True when the preview would draw nothing identifiable, so the canvas keeps the type
    /// label. Tables and charts are never labelled: a table always draws a stub — its real
    /// columns, or the placeholder column — and a chart always draws its "No data" box at its
    /// configured height, so both already read as themselves. TableElement.Binding is required
    /// and non-nullable, so there is no "table with nothing at all" case to detect.
    /// </summary>
    public static bool IsEmpty(ReportElement element) => element switch
    {
        TextElement text => string.IsNullOrWhiteSpace(text.Content),
        SectionElement section => section.Children is not { Count: > 0 },
        _ => false,
    };

    private static IEnumerable<ReportElement> Flatten(IEnumerable<ReportElement> elements)
    {
        foreach (var element in elements)
        {
            // A list element can be null even where its type isn't (see the note in Resolve).
            if (element is null)
                continue;
            yield return element;
            if (element is SectionElement { Children: { } children })
                foreach (var child in Flatten(children))
                    yield return child;
        }
    }

    /// <summary>
    /// One header row and one body row. The body cell holds the column's own Field, so the
    /// canvas answers "which column lands here" while the browser's own table layout supplies
    /// the real column widths and row height. A table whose columns come from the query cannot
    /// know them until it runs, so it previews as a single named placeholder column.
    /// </summary>
    private static ReportDataTable StubTable(TableElement table)
    {
        static ReportDataTable Placeholder()
        {
            var placeholder = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                [PlaceholderColumn] = string.Empty,
            };
            return new ReportDataTable([PlaceholderColumn], [placeholder]);
        }

        // Null Columns falls back to the same placeholder as the zero-column case (see the
        // note in Resolve for why a non-required List<T> = [] doesn't rule this out).
        if (table.Columns is not { Count: > 0 } columns)
            return Placeholder();

        // A null entry inside Columns, or one with "field": null / "": all dropped before
        // Field is ever dereferenced — a caller-authored column missing what it needs to
        // preview is not previewable, not a crash.
        var usable = columns.Where(column => column is not null && !string.IsNullOrWhiteSpace(column.Field)).ToList();
        if (usable.Count == 0)
            return Placeholder();

        // Two columns may legitimately bind the same Field (the same value formatted two
        // ways), so the row is a dictionary keyed by field — assigning twice is harmless, and
        // ReportTable iterates the column DEFINITIONS rather than these keys anyway.
        var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var column in usable)
            row[column.Field] = column.Field;

        return new ReportDataTable([.. usable.Select(column => column.Field)], [row]);
    }

    private const string PlaceholderColumn = "(columns from data)";
}
