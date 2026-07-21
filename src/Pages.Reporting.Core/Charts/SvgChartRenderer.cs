using System.Globalization;
using System.Text;
using Pages.Reporting.Core.Model;
using Pages.Reporting.Core.Rendering;

namespace Pages.Reporting.Core.Charts;

/// <summary>
/// Renders chart elements to self-contained SVG markup. The same SVG is inlined by the
/// Blazor components and embedded in the exported HTML, so charts are pixel-identical on
/// screen and in PDF. Colors reference CSS variables (--pr-series-N etc.) with light-mode
/// fallbacks, so themes can restyle charts without regenerating them.
/// </summary>
public static class SvgChartRenderer
{
    private const int Width = 800;

    // Validated categorical palette (light-mode values double as PDF fallbacks).
    private static readonly string[] Palette =
    [
        "#2a78d6", "#1baf7a", "#eda100", "#008300", "#4a3aa7", "#e34948", "#e87ba4", "#eb6834",
    ];

    private const string Grid = "var(--pr-grid, #e1e0d9)";
    private const string Baseline = "var(--pr-baseline, #c3c2b7)";
    private const string Muted = "var(--pr-muted, #898781)";
    private const string SecondaryInk = "var(--pr-ink-secondary, #52514e)";
    private const string Surface = "var(--pr-surface, #fcfcfb)";
    private const string FontFamily = "system-ui, -apple-system, 'Segoe UI', sans-serif";

    private static string SeriesFill(int index) => $"var(--pr-series-{index % Palette.Length + 1}, {Palette[index % Palette.Length]})";

    public static string Render(ChartElement chart, ReportDataTable? table)
    {
        var height = Math.Max(chart.Height, 120);
        if (table is null || table.Rows.Count == 0)
            return EmptySvg(height, "No data");

        var labelField = chart.LabelField;
        var valueFields = chart.ValueFields.Count > 0
            ? chart.ValueFields
            : table.Columns.Where(c =>
                    !string.Equals(c, labelField, StringComparison.OrdinalIgnoreCase) &&
                    table.Rows.Any(r => ValueFormatter.IsNumeric(r.GetValueOrDefault(c))))
                .ToList();

        if (valueFields.Count == 0)
            return EmptySvg(height, "No numeric fields to plot");

        var labels = table.Rows.Select(r => ValueFormatter.Format(r.GetValueOrDefault(labelField), null)).ToList();
        var series = valueFields
            .Select(f => table.Rows.Select(r => ValueFormatter.ToDouble(r.GetValueOrDefault(f))).ToArray())
            .ToList();

        return chart.ChartType switch
        {
            ChartType.Pie => RenderPie(chart, height, labels, series[0], valueFields[0]),
            ChartType.Line => RenderCartesian(chart, height, labels, valueFields, series, line: true),
            _ => RenderCartesian(chart, height, labels, valueFields, series, line: false),
        };
    }

    // ---- cartesian (bar / line) ------------------------------------------------------

    private static string RenderCartesian(
        ChartElement chart, int height, List<string> labels,
        List<string> valueFields, List<double[]> series, bool line)
    {
        var svg = new StringBuilder();
        OpenSvg(svg, height);

        var legendHeight = valueFields.Count > 1 ? LegendHeight(valueFields) : 0;
        var plotLeft = 56.0;
        var plotRight = Width - 16.0;
        var plotTop = 12.0 + legendHeight;
        var plotBottom = height - 28.0;
        var plotWidth = plotRight - plotLeft;
        var plotHeight = plotBottom - plotTop;

        if (valueFields.Count > 1)
            DrawLegend(svg, valueFields);

        var maxValue = series.SelectMany(s => s).DefaultIfEmpty(0).Max();
        var minValue = Math.Min(0, series.SelectMany(s => s).DefaultIfEmpty(0).Min());
        var (niceMin, niceMax, step) = NiceScale(minValue, maxValue);
        double YFor(double v) => plotBottom - (v - niceMin) / (niceMax - niceMin) * plotHeight;

        // hairline gridlines + tick labels (clean numbers, thousands-comma'd, muted ink)
        for (var tick = niceMin; tick <= niceMax + step / 2; tick += step)
        {
            var y = YFor(tick);
            svg.Append($"<line x1='{F(plotLeft)}' y1='{F(y)}' x2='{F(plotRight)}' y2='{F(y)}' stroke='{Grid}' stroke-width='1'/>");
            svg.Append(Text(plotLeft - 8, y + 4, FormatTick(tick, chart.Format), 11, Muted, "end"));
        }

        // baseline
        var baselineY = YFor(Math.Max(niceMin, 0));
        svg.Append($"<line x1='{F(plotLeft)}' y1='{F(baselineY)}' x2='{F(plotRight)}' y2='{F(baselineY)}' stroke='{Baseline}' stroke-width='1'/>");

        var bandWidth = plotWidth / labels.Count;

        // x labels — skip evenly when they would collide
        var every = Math.Max(1, (int)Math.Ceiling(labels.Count * 60.0 / plotWidth));
        for (var i = 0; i < labels.Count; i += every)
        {
            var x = plotLeft + bandWidth * (i + 0.5);
            svg.Append(Text(x, plotBottom + 16, Truncate(labels[i], (int)(bandWidth * every / 6.5)), 11, Muted, "middle"));
        }

        if (line)
            DrawLines(svg, chart, labels.Count, series, plotLeft, bandWidth, YFor, plotRight);
        else
            DrawColumns(svg, labels.Count, series, plotLeft, bandWidth, YFor, baselineY);

        svg.Append("</svg>");
        return svg.ToString();
    }

    private static void DrawColumns(
        StringBuilder svg, int categories, List<double[]> series,
        double plotLeft, double bandWidth, Func<double, double> yFor, double baselineY)
    {
        var seriesCount = series.Count;
        // ≤24px thick, 2px surface gap between neighbors, band keeps breathing room
        var gap = 2.0;
        var barWidth = Math.Min(24.0, (bandWidth * 0.7 - gap * (seriesCount - 1)) / seriesCount);
        if (barWidth < 2) barWidth = Math.Max(1.5, bandWidth / seriesCount - gap);
        var groupWidth = barWidth * seriesCount + gap * (seriesCount - 1);

        for (var s = 0; s < seriesCount; s++)
        {
            for (var i = 0; i < categories; i++)
            {
                var value = series[s][i];
                var x = plotLeft + bandWidth * i + (bandWidth - groupWidth) / 2 + s * (barWidth + gap);
                var yValue = yFor(value);
                var top = Math.Min(yValue, baselineY);
                var barHeight = Math.Abs(baselineY - yValue);
                if (barHeight < 0.5) continue;
                svg.Append(ColumnPath(x, top, barWidth, barHeight, SeriesFill(s), roundTop: value >= 0));
            }
        }
    }

    /// <summary>Column with a 4px rounded data-end and a square baseline end.</summary>
    private static string ColumnPath(double x, double y, double w, double h, string fill, bool roundTop)
    {
        var r = Math.Min(4.0, Math.Min(w / 2, h));
        string d;
        if (roundTop)
        {
            d = $"M {F(x)} {F(y + h)} L {F(x)} {F(y + r)} Q {F(x)} {F(y)} {F(x + r)} {F(y)} " +
                $"L {F(x + w - r)} {F(y)} Q {F(x + w)} {F(y)} {F(x + w)} {F(y + r)} L {F(x + w)} {F(y + h)} Z";
        }
        else
        {
            d = $"M {F(x)} {F(y)} L {F(x + w)} {F(y)} L {F(x + w)} {F(y + h - r)} Q {F(x + w)} {F(y + h)} {F(x + w - r)} {F(y + h)} " +
                $"L {F(x + r)} {F(y + h)} Q {F(x)} {F(y + h)} {F(x)} {F(y + h - r)} Z";
        }
        return $"<path d='{d}' fill='{fill}'/>";
    }

    private static void DrawLines(
        StringBuilder svg, ChartElement chart, int categories, List<double[]> series,
        double plotLeft, double bandWidth, Func<double, double> yFor, double plotRight)
    {
        for (var s = 0; s < series.Count; s++)
        {
            var points = new StringBuilder();
            for (var i = 0; i < categories; i++)
            {
                var x = plotLeft + bandWidth * (i + 0.5);
                points.Append($"{F(x)},{F(yFor(series[s][i]))} ");
            }

            svg.Append($"<polyline points='{points}' fill='none' stroke='{SeriesFill(s)}' " +
                       "stroke-width='2' stroke-linejoin='round' stroke-linecap='round'/>");

            // ≥8px markers with a 2px surface ring so they stay legible over other lines
            var markerEvery = Math.Max(1, (int)Math.Ceiling(categories / 40.0));
            for (var i = 0; i < categories; i += markerEvery)
            {
                var x = plotLeft + bandWidth * (i + 0.5);
                svg.Append($"<circle cx='{F(x)}' cy='{F(yFor(series[s][i]))}' r='4' fill='{SeriesFill(s)}' " +
                           $"stroke='{Surface}' stroke-width='2'/>");
            }

            // selective direct label: the end value only
            var lastX = plotLeft + bandWidth * (categories - 0.5);
            var endLabel = FormatTick(series[s][^1], chart.Format);
            var anchorX = Math.Min(lastX + 8, plotRight);
            svg.Append(Text(anchorX, yFor(series[s][^1]) + 4, endLabel, 11, SecondaryInk, "start"));
        }
    }

    // ---- pie -------------------------------------------------------------------------

    private static string RenderPie(ChartElement chart, int height, List<string> labels, double[] values, string valueField)
    {
        var svg = new StringBuilder();
        OpenSvg(svg, height);

        var total = values.Where(v => v > 0).Sum();
        if (total <= 0)
        {
            svg.Append(Text(Width / 2.0, height / 2.0, "No data", 13, Muted, "middle"));
            svg.Append("</svg>");
            return svg.ToString();
        }

        var cx = height * 0.52;
        var cy = height / 2.0;
        var radius = Math.Min(height / 2.0 - 20, 150);

        var angle = -Math.PI / 2;
        for (var i = 0; i < values.Length; i++)
        {
            if (values[i] <= 0) continue;
            var sweep = values[i] / total * Math.PI * 2;
            var end = angle + sweep;
            svg.Append(SlicePath(cx, cy, radius, angle, end, SeriesFill(i)));

            // percentage inside slices that can fit it; legend carries the rest
            if (sweep > 0.45)
            {
                var mid = angle + sweep / 2;
                var lx = cx + Math.Cos(mid) * radius * 0.62;
                var ly = cy + Math.Sin(mid) * radius * 0.62;
                var inkOnFill = LightFill(i) ? "#0b0b0b" : "#ffffff";
                svg.Append(Text(lx, ly + 4, (values[i] / total).ToString("P0", CultureInfo.CurrentCulture), 12, inkOnFill, "middle"));
            }

            angle = end;
        }

        // legend: swatch + label + formatted value (text in ink tokens, never series color)
        var legendX = cx + radius + 32;
        var legendY = cy - Math.Min(labels.Count, 8) * 22 / 2.0 + 8;
        for (var i = 0; i < labels.Count && i < 8; i++)
        {
            var y = legendY + i * 22;
            svg.Append($"<rect x='{F(legendX)}' y='{F(y - 9)}' width='10' height='10' rx='2' fill='{SeriesFill(i)}'/>");
            var text = $"{Truncate(labels[i], 24)}  {FormatTick(values[i], chart.Format)}";
            svg.Append(Text(legendX + 16, y, text, 12, SecondaryInk, "start"));
        }

        svg.Append("</svg>");
        return svg.ToString();
    }

    private static string SlicePath(double cx, double cy, double r, double start, double end, string fill)
    {
        var x1 = cx + Math.Cos(start) * r;
        var y1 = cy + Math.Sin(start) * r;
        var x2 = cx + Math.Cos(end) * r;
        var y2 = cy + Math.Sin(end) * r;
        var largeArc = end - start > Math.PI ? 1 : 0;
        // 2px surface-color stroke = the surface gap between touching slices
        return $"<path d='M {F(cx)} {F(cy)} L {F(x1)} {F(y1)} A {F(r)} {F(r)} 0 {largeArc} 1 {F(x2)} {F(y2)} Z' " +
               $"fill='{fill}' stroke='{Surface}' stroke-width='2'/>";
    }

    private static bool LightFill(int index) => (index % Palette.Length) is 1 or 2 or 6; // aqua, yellow, magenta

    // ---- shared ----------------------------------------------------------------------

    private static void OpenSvg(StringBuilder svg, int height)
    {
        svg.Append($"<svg viewBox='0 0 {Width} {height}' xmlns='http://www.w3.org/2000/svg' role='img' " +
                   $"style=\"width:100%;height:auto;font-family:{FontFamily}\">");
    }

    private static string EmptySvg(int height, string message)
    {
        var svg = new StringBuilder();
        OpenSvg(svg, height);
        svg.Append(Text(Width / 2.0, height / 2.0, message, 13, Muted, "middle"));
        svg.Append("</svg>");
        return svg.ToString();
    }

    private static int LegendHeight(List<string> valueFields) => 24 * (int)Math.Ceiling(EstimateLegendWidth(valueFields) / (Width - 32.0));

    private static double EstimateLegendWidth(List<string> valueFields) =>
        valueFields.Sum(f => 16 + f.Length * 7.0 + 16);

    private static void DrawLegend(StringBuilder svg, List<string> valueFields)
    {
        var x = 56.0;
        var y = 14.0;
        for (var i = 0; i < valueFields.Count; i++)
        {
            var itemWidth = 16 + valueFields[i].Length * 7.0 + 16;
            if (x + itemWidth > Width - 16)
            {
                x = 56.0;
                y += 24;
            }
            svg.Append($"<rect x='{F(x)}' y='{F(y - 9)}' width='10' height='10' rx='2' fill='{SeriesFill(i)}'/>");
            svg.Append(Text(x + 16, y, valueFields[i], 12, SecondaryInk, "start"));
            x += itemWidth;
        }
    }

    private static string Text(double x, double y, string content, int size, string fill, string anchor) =>
        $"<text x='{F(x)}' y='{F(y)}' font-size='{size}' fill='{fill}' text-anchor='{anchor}'>{Escape(content)}</text>";

    private static string FormatTick(double value, string? format) =>
        ValueFormatter.Format(value, string.IsNullOrEmpty(format) ? "#,0.##" : format);

    private static (double Min, double Max, double Step) NiceScale(double min, double max)
    {
        if (max <= min) max = min + 1;
        var range = max - min;
        var rawStep = range / 5;
        var magnitude = Math.Pow(10, Math.Floor(Math.Log10(rawStep)));
        var normalized = rawStep / magnitude;
        var step = magnitude * (normalized <= 1 ? 1 : normalized <= 2 ? 2 : normalized <= 5 ? 5 : 10);
        var niceMin = Math.Floor(min / step) * step;
        var niceMax = Math.Ceiling(max / step) * step;
        return (niceMin, niceMax, step);
    }

    private static string Truncate(string value, int maxChars) =>
        maxChars > 1 && value.Length > maxChars ? value[..(maxChars - 1)] + "…" : value;

    private static string Escape(string value) => value
        .Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("'", "&apos;");

    private static string F(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);
}
