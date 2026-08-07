using Pages.Reporting.Core.Model;

namespace Pages.Reporting.Blazor;

/// <summary>
/// Inline CSS for presenting a report as its real paper sheet on screen: width and
/// minimum height from the page size/orientation, padding from the print margin.
/// Used by the designer's canvas/preview; hosts can use it to frame ReportView the
/// same way, so on-screen pages match the PDF dimensions.
/// </summary>
public static class PageSheet
{
    private static readonly Dictionary<string, (double Width, double Height)> PaperSizes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["A4"] = (210, 297),
            ["A3"] = (297, 420),
            ["Letter"] = (215.9, 279.4),
            ["Legal"] = (215.9, 355.6),
        };

    /// <summary>Paper dimensions in millimetres, orientation applied. Shared with the PDF exporter.</summary>
    public static (double Width, double Height) SizeMm(PageSettings page)
    {
        var (width, height) = PaperSizes.TryGetValue(page.Size, out var size) ? size : (210d, 297d);
        return page.Landscape ? (height, width) : (width, height);
    }

    /// <summary>
    /// The four print margins in millimetres, from the authored CSS shorthand. The margin
    /// stays an authored string (it keeps its 1-to-4-value form), so it is parsed here — once,
    /// in the one place every consumer reads. Three separate readers previously disagreed:
    /// one took the shorthand's second value as "the" margin (that is `right`), one could not
    /// parse a shorthand at all, and one refused to draw when it saw one.
    /// </summary>
    public static (double Top, double Right, double Bottom, double Left) MarginsMm(PageSettings page)
    {
        var parts = (page.Margin ?? string.Empty)
            .Trim()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);

        // CSS shorthand order: 1 = all; 2 = vertical horizontal; 3 = top horizontal bottom;
        // 4 = top right bottom left.
        return parts.Length switch
        {
            0 => (15, 15, 15, 15),
            1 => Uniform(CssLength.ToMm(parts[0], 15)),
            2 => (CssLength.ToMm(parts[0], 15), CssLength.ToMm(parts[1], 15),
                  CssLength.ToMm(parts[0], 15), CssLength.ToMm(parts[1], 15)),
            3 => (CssLength.ToMm(parts[0], 15), CssLength.ToMm(parts[1], 15),
                  CssLength.ToMm(parts[2], 15), CssLength.ToMm(parts[1], 15)),
            _ => (CssLength.ToMm(parts[0], 15), CssLength.ToMm(parts[1], 15),
                  CssLength.ToMm(parts[2], 15), CssLength.ToMm(parts[3], 15)),
        };

        static (double, double, double, double) Uniform(double all) => (all, all, all, all);
    }

    /// <summary>
    /// The gap between bands in millimetres; an explicit BandGapMm wins over the default of 0.
    /// Bands abut by default because a band is already a box the author positions inside — space
    /// before or after a component is space they left in the band, and it prints. A second,
    /// implicit gap between bands would be spacing the canvas shows but the author never placed.
    /// </summary>
    public static double BandGapMm(Report report) =>
        report.BandGapMm ?? 0;

    public static string Css(PageSettings page)
    {
        var (width, height) = SizeMm(page);
        var margin = string.IsNullOrWhiteSpace(page.Margin) ? "15mm" : page.Margin;
        // Flex column so the page-style box (StyleCss.Page) can stretch to the sheet's
        // full height — the page border frames the whole page, like the PDF's frame.
        return FormattableString.Invariant($"width:{width}mm;min-height:{height}mm;padding:{margin};box-sizing:border-box;display:flex;flex-direction:column;");
    }

    /// <summary>
    /// Screen-only pagination guide: inline CSS for an overlay inside the report root
    /// drawing a faint line wherever the PDF will start a new page (page height minus
    /// the print margins, the per-page page padding, and the footer reserve — the same
    /// numbers the exporter uses). Approximate: Chromium moves an unbreakable component
    /// (a chart, a table) wholly onto the next page, which shifts later breaks down.
    /// Rendered by ReportView on screen; hide it with CSS on .pr-page-break if unwanted.
    /// </summary>
    public static string? BreakGuideCss(PageSettings page, ElementStyle? style, IEnumerable<PageBand>? bands = null)
    {
        var (_, height) = SizeMm(page);
        var margins = MarginsMm(page);
        var (padTop, padBottom) = VerticalPadding(style?.Padding);
        var capacity = FormattableString.Invariant($"{height - margins.Top - margins.Bottom:0.###}mm");
        if (page.ShowFooter)
            capacity += FormattableString.Invariant($" - {PageSettings.FooterReserveMm}mm");
        // PageHeader/PageFooter bands print inside the page margin, so they shrink every
        // page's usable height by exactly their declared height.
        if (bands is not null)
            foreach (var band in bands.Where(b => BandTypes.IsMargin(b.Type)))
                capacity += FormattableString.Invariant($" - {band.HeightMm}mm");
        if (padTop is not null)
            capacity += $" - {padTop} - {padBottom}";

        // The overlay spans the content region (below the page padding), so each
        // capacity-sized background tile ends exactly where a PDF page ends; tiles
        // above the top edge are clipped, leaving no line at the content start.
        return $"position:absolute;left:0;right:0;bottom:0;top:{padTop ?? "0"};pointer-events:none;z-index:-1;" +
               "background-image:linear-gradient(to bottom,transparent calc(100% - 1px),rgba(15,15,15,0.28) calc(100% - 1px));" +
               $"background-size:100% calc({capacity});";
    }

    /// <summary>Top/bottom lengths of a CSS padding shorthand (bare numbers → px).</summary>
    private static (string? Top, string? Bottom) VerticalPadding(string? padding)
    {
        if (string.IsNullOrWhiteSpace(padding))
            return (null, null);
        var parts = padding.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => StyleCss.Length(part)!)
            .ToArray();
        return parts.Length switch
        {
            0 => (null, null),
            1 or 2 => (parts[0], parts[0]),
            _ => (parts[0], parts[2]),
        };
    }
}
