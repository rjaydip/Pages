using ClosedXML.Excel;
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
    /// <paramref name="parameters"/> overrides the report's parameter defaults.
    /// </summary>
    public async Task<byte[]> ExportAsync(
        string reportJson,
        IReadOnlyDictionary<string, string?>? parameters = null,
        CancellationToken cancellationToken = default) =>
        Export(await generator.GenerateAsync(reportJson, parameters, cancellationToken));

    public byte[] Export(ResolvedReport report)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet(SheetName(report.Definition.Name));
        var row = 1;

        foreach (var element in report.Definition.Elements)
            row = WriteElement(sheet, report, element, row);

        sheet.Columns().AdjustToContents(1, Math.Min(row, 500));

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private int WriteElement(IXLWorksheet sheet, ResolvedReport report, ReportElement element, int row)
    {
        var data = report.GetData(element);

        switch (element)
        {
            case TextElement:
                sheet.Cell(row, 1).SetValue(data.Scalar?.ToString() ?? string.Empty);
                return row + 2;

            case TitleElement title:
                sheet.Cell(row, 1).SetValue(title.Text).Style.Font.SetBold().Font.SetFontSize(16);
                row++;
                if (!string.IsNullOrEmpty(title.Subtitle))
                {
                    sheet.Cell(row, 1).SetValue(title.Subtitle).Style.Font.SetFontColor(XLColor.Gray);
                    row++;
                }
                return row + 1;

            case ValueElement value:
                return WriteScalar(sheet, row, value.Label, data, value.Format);

            case KpiCardElement kpi:
                return WriteScalar(sheet, row, kpi.Label, data, kpi.Format);

            case TableElement table:
                return WriteTable(sheet, row, table.Title, data,
                    table.Columns.Count > 0 ? table.Columns : null);

            case ChartElement chart:
                return WriteTable(sheet, row, chart.Title ?? "Chart data", data, null);

            case SectionElement section:
                if (!string.IsNullOrEmpty(section.Title))
                {
                    sheet.Cell(row, 1).SetValue(section.Title).Style.Font.SetBold().Font.SetFontSize(13);
                    row += 2;
                }
                foreach (var child in section.Children)
                    row = WriteElement(sheet, report, child, row);
                return row;

            default:
                return row;
        }
    }

    private static int WriteScalar(IXLWorksheet sheet, int row, string label, ResolvedData data, string? format)
    {
        sheet.Cell(row, 1).SetValue(label).Style.Font.SetBold();
        if (data.Error is not null)
            sheet.Cell(row, 2).SetValue($"error: {data.Error}").Style.Font.SetFontColor(XLColor.Red);
        else
            SetTypedValue(sheet.Cell(row, 2), data.Scalar, format);
        return row + 2;
    }

    private static int WriteTable(IXLWorksheet sheet, int row, string? title, ResolvedData data, List<ColumnDefinition>? columns)
    {
        if (!string.IsNullOrEmpty(title))
        {
            sheet.Cell(row, 1).SetValue(title).Style.Font.SetBold().Font.SetFontSize(13);
            row++;
        }

        if (data.Error is not null)
        {
            sheet.Cell(row, 1).SetValue($"error: {data.Error}").Style.Font.SetFontColor(XLColor.Red);
            return row + 2;
        }

        var table = data.Table;
        if (table is null || table.Columns.Count == 0)
            return row + 1;

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

        return row + 1;
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

    private static string SheetName(string name)
    {
        var invalid = new[] { ':', '\\', '/', '?', '*', '[', ']' };
        var clean = new string(name.Where(ch => !invalid.Contains(ch)).ToArray()).Trim();
        if (clean.Length == 0) clean = "Report";
        return clean.Length > 31 ? clean[..31] : clean;
    }
}
