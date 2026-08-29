using ClosedXML.Excel;
using Pages.Reporting.Core.Data;
using Pages.Reporting.Core.Model;
using Pages.Reporting.Core.Rendering;

namespace Pages.Reporting.Export;

/// <summary>
/// Exports a report's data to an Excel workbook (bytes). Excel export is data-faithful
/// rather than pixel-faithful: tables, values, and chart source data with typed cells.
/// </summary>
public sealed class ExcelReportExporter(IReportGenerator generator)
{
    /// <summary>
    /// Generates the report from its stored JSON and returns the .xlsx as bytes.
    /// <paramref name="options"/> carries parameter overrides and runtime connection strings.
    /// </summary>
    public async Task<byte[]> ExportAsync(
        string reportJson,
        ReportRuntimeOptions? options = null,
        CancellationToken cancellationToken = default) =>
        Export(await generator.GenerateAsync(reportJson, options, cancellationToken));

    public byte[] Export(ResolvedReport report)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet(SheetName(report.Definition.Name));
        var row = 1;

        // The same plan the screen and the PDF render, flattened: a group's bands repeat once
        // per value, so the sheet reads in the same order the printed report does. The margin
        // bands (PageHeader/PageFooter) are print-time furniture with no sheet equivalent
        // (ClosedXML's PageSetup.Header/Footer takes plain strings, not layout) — BandPlan
        // already excludes them.
        // Excel styles every cell independently — there is no inheritance to lean on, so the
        // font that cascades on screen has to be carried down the writer by hand.
        var reportFont = ExcelFontName(report.Definition.Style?.FontFamily);

        foreach (var node in BandPlan.Build(report))
            row = WriteBand(sheet, report, node, row, reportFont);

        sheet.Columns().AdjustToContents(1, Math.Min(row, 500));

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private int WriteBand(IXLWorksheet sheet, ResolvedReport report, BandPlanNode node, int row, string? font)
    {
        // A Table band holds exactly one TableElement — matching what PageBandView renders,
        // so hand-authored JSON with extra elements in that band doesn't reach Excel either.
        // Every other band's element order is creation order, not the visual order the reader
        // sees on screen/PDF — sort by the same rectangle the renderer positions by. An
        // unpositioned element (Position null) sorts last rather than first.
        var elements = node.Band.Type == BandType.Table
            ? node.Band.Elements.Where(element => element is TableElement).Take(1)
            : node.Band.Elements.OrderBy(element => element.Position?.Y ?? double.MaxValue)
                .ThenBy(element => element.Position?.X ?? double.MaxValue);

        var bandFont = ExcelFontName(node.Band.Style?.FontFamily) ?? font;
        foreach (var element in elements)
            row = WriteElement(sheet, report, element, row, node.Scope, bandFont);
        return row;
    }

    private int WriteElement(IXLWorksheet sheet, ResolvedReport report, ReportElement element, int row, GroupScope? scope, string? font)
    {
        var data = report.GetData(element);
        var elementFont = ExcelFontName(element.Style?.FontFamily) ?? font;

        switch (element)
        {
            case TextElement text:
            {
                // Page-number placeholders are print-only; a spreadsheet gets plain text,
                // never the raw placeholder characters. Inside a group the template is
                // re-rendered against that group's rows, as the printed report does.
                var cell = sheet.Cell(row, 1);
                cell.SetValue(PageTokens.Resolve(
                    scope is null
                        ? data.Scalar?.ToString()
                        : ScopedValues.Render(report, scope, text.Content, htmlContext: false),
                    PageTokenMode.Plain));
                if (elementFont is not null)
                    cell.Style.Font.FontName = elementFont;
                return row + 2;
            }

            case TableElement table:
                return WriteTable(sheet, row, table.Title, ScopedData(data, table, scope),
                    table.Columns.Count > 0 ? table.Columns : null, elementFont);

            case ChartElement chart:
                return WriteTable(sheet, row, chart.Title ?? "Chart data", data, null, elementFont);

            case SectionElement section:
                if (!string.IsNullOrEmpty(section.Title))
                {
                    var titleCell = sheet.Cell(row, 1);
                    titleCell.SetValue(section.Title).Style.Font.SetBold().Font.SetFontSize(13);
                    if (elementFont is not null)
                        titleCell.Style.Font.FontName = elementFont;
                    row += 2;
                }
                foreach (var child in section.Children)
                    row = WriteElement(sheet, report, child, row, scope, elementFont);
                return row;

            default:
                return row;
        }
    }

    /// <summary>A table inside a group writes that group's slice, matching the printed report.</summary>
    private static ResolvedData ScopedData(ResolvedData data, TableElement table, GroupScope? scope) =>
        data.Table is { } rows
        && table.Binding is DataSetBinding reference
        && scope?.For(reference.Name) is { } match
            ? new ResolvedData { Table = new ReportDataTable(rows.Columns, match.Rows), Error = data.Error }
            : data;

    private static int WriteTable(IXLWorksheet sheet, int row, string? title, ResolvedData data, List<ColumnDefinition>? columns, string? font)
    {
        var startRow = row;

        // One range assignment per block, not one per cell: ClosedXML iterates a range's cells
        // internally too, so this trades N interleaved per-cell style writes for one pass —
        // fewer calls through the API, not an order-of-magnitude win.
        int Finish(int next, int columnCount)
        {
            // next is always "last content row + 2" (a blank spacer row separates blocks), so
            // the last content row is next - 2. Style up to there, not next - 1, or the blank
            // spacer picks up the font too and extends the sheet's used range by a row. When
            // there was neither a title nor any columns, next - 2 falls below startRow — skip
            // the range write rather than pass sheet.Range an inverted/zero-width extent.
            var lastContentRow = next - 2;
            if (font is not null && lastContentRow >= startRow)
                sheet.Range(startRow, 1, lastContentRow, Math.Max(1, columnCount)).Style.Font.FontName = font;
            return next;
        }

        if (!string.IsNullOrEmpty(title))
        {
            sheet.Cell(row, 1).SetValue(title).Style.Font.SetBold().Font.SetFontSize(13);
            row++;
        }

        if (data.Error is not null)
        {
            sheet.Cell(row, 1).SetValue($"error: {data.Error}").Style.Font.SetFontColor(XLColor.Red);
            return Finish(row + 2, 1);
        }

        var table = data.Table;
        if (table is null || table.Columns.Count == 0)
            return Finish(row + 1, 1);

        var effectiveColumns = columns
            ?? table.Columns.Select(c => new ColumnDefinition { Field = c }).ToList();

        for (var c = 0; c < effectiveColumns.Count; c++)
        {
            var cell = sheet.Cell(row, c + 1);
            cell.SetValue(effectiveColumns[c].Header ?? effectiveColumns[c].Field);
            cell.Style.Font.SetBold();
            cell.Style.Fill.SetBackgroundColor(XLColor.FromHtml("#efeeea"));
        }
        row++;

        foreach (var dataRow in table.Rows)
        {
            for (var c = 0; c < effectiveColumns.Count; c++)
                SetTypedValue(sheet.Cell(row, c + 1), dataRow.GetValueOrDefault(effectiveColumns[c].Field), effectiveColumns[c].Format);
            row++;
        }

        if (effectiveColumns.Any(c => c.Aggregate != ColumnAggregate.None))
        {
            for (var c = 0; c < effectiveColumns.Count; c++)
            {
                var column = effectiveColumns[c];
                var cell = sheet.Cell(row, c + 1);
                cell.Style.Font.SetBold();
                if (column.Aggregate == ColumnAggregate.None)
                    continue;

                var values = table.Rows
                    .Select(r => r.GetValueOrDefault(column.Field))
                    .Where(v => v is not null)
                    .ToList();

                if (column.Aggregate == ColumnAggregate.Count)
                {
                    cell.SetValue(values.Count);
                    continue;
                }

                var numbers = values.Where(ValueFormatter.IsNumeric).Select(ValueFormatter.ToDouble).ToList();
                if (numbers.Count == 0)
                    continue;

                var result = column.Aggregate switch
                {
                    ColumnAggregate.Sum => numbers.Sum(),
                    ColumnAggregate.Average => numbers.Average(),
                    ColumnAggregate.Min => numbers.Min(),
                    ColumnAggregate.Max => numbers.Max(),
                    _ => 0,
                };
                SetTypedValue(cell, result, column.Format ?? "#,0.##");
            }
            row++;
        }

        return Finish(row + 1, effectiveColumns.Count);
    }

    private static void SetTypedValue(IXLCell cell, object? value, string? format)
    {
        switch (value)
        {
            case null:
                return;
            case DateTime dateTime:
                cell.SetValue(dateTime);
                break;
            case bool boolean:
                cell.SetValue(boolean);
                break;
            case { } when ValueFormatter.IsNumeric(value):
                cell.SetValue(ValueFormatter.ToDouble(value));
                break;
            default:
                cell.SetValue(value.ToString());
                return;
        }

        if (ExcelFormat(format) is { } numberFormat)
            cell.Style.NumberFormat.Format = numberFormat;
    }

    /// <summary>Maps common .NET format strings (N2, C, P0, #,0.##, d) to Excel number formats.</summary>
    private static string? ExcelFormat(string? format)
    {
        if (string.IsNullOrEmpty(format))
            return null;

        var decimals = char.IsDigit(format[^1]) ? format[^1] - '0' : (int?)null;
        return char.ToUpperInvariant(format[0]) switch
        {
            'N' => Thousands(decimals ?? 2),
            'C' => "\"$\"" + Thousands(decimals ?? 2),
            'P' => decimals is > 0 ? "0." + new string('0', decimals.Value) + "%" : "0%",
            'D' when format is "d" or "D" => "yyyy-mm-dd",
            '#' or '0' => Thousands(format.Contains('.') ? format.Length - format.IndexOf('.') - 1 : 0),
            _ => null,
        };

        static string Thousands(int decimals) =>
            decimals > 0 ? "#,##0." + new string('0', decimals) : "#,##0";
    }

    /// <summary>
    /// CSS keywords that name no installed font. Vendor keywords (-apple-system) are skipped
    /// by their leading dash rather than listed.
    /// </summary>
    private static readonly HashSet<string> GenericFamilies = new(StringComparer.OrdinalIgnoreCase)
    {
        "serif", "sans-serif", "monospace", "cursive", "fantasy", "system-ui",
        "ui-serif", "ui-sans-serif", "ui-monospace", "ui-rounded", "math", "emoji", "fangsong",
    };

    /// <summary>
    /// Reduces a CSS font stack to the one family name Excel can take. Unlike the PDF, the
    /// substitution here happens on the machine opening the workbook, so a stack of nothing
    /// but generics returns null and Excel keeps its own default rather than being given a
    /// keyword it would treat as a missing font.
    /// </summary>
    private static string? ExcelFontName(string? fontFamily)
    {
        if (string.IsNullOrWhiteSpace(fontFamily))
            return null;

        foreach (var part in fontFamily.Split(','))
        {
            var name = part.Trim().Trim('\'', '"').Trim();
            if (name.Length == 0 || name[0] == '-' || GenericFamilies.Contains(name))
                continue;
            return name;
        }
        return null;
    }

    private static string SheetName(string name)
    {
        var invalid = new[] { ':', '\\', '/', '?', '*', '[', ']' };
        var clean = new string(name.Where(ch => !invalid.Contains(ch)).ToArray()).Trim();
        if (clean.Length == 0) clean = "Report";
        return clean.Length > 31 ? clean[..31] : clean;
    }
}
