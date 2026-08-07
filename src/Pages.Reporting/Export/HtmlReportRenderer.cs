using System.Text;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Pages.Reporting.Blazor;
using Pages.Reporting.Blazor.Components;
using Pages.Reporting.Core.Model;
using Pages.Reporting.Core.Rendering;

namespace Pages.Reporting.Export;

/// <summary>
/// Renders a resolved report to a self-contained HTML document using the SAME Blazor
/// components as the on-screen view (in print mode). The library ships no styling:
/// whatever CSS the caller supplies is inlined verbatim, so screen and PDF match when
/// the caller passes the same stylesheet they use on screen.
/// </summary>
public sealed class HtmlReportRenderer(IServiceScopeFactory scopeFactory, ILoggerFactory loggerFactory)
{
    public async Task<string> RenderAsync(ResolvedReport report, string? css = null, CancellationToken cancellationToken = default)
    {
        // HtmlRenderer resolves scoped services, so give it a scope of its own.
        using var scope = scopeFactory.CreateScope();
        await using var renderer = new HtmlRenderer(scope.ServiceProvider, loggerFactory);

        var body = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var parameters = ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                [nameof(ReportView.Resolved)] = report,
                [nameof(ReportView.PrintMode)] = true,
            });
            var root = await renderer.RenderComponentAsync<ReportView>(parameters);
            return root.ToHtmlString();
        });

        return WrapDocument(report, body, css);
    }

    /// <summary>
    /// Renders every band of the given <paramref name="type"/> (PageHeader or PageFooter —
    /// the two margin band types, printed on every page) as a self-contained fragment for
    /// Chromium's PDF header/footer template. Those templates are isolated documents — they
    /// don't inherit the page's stylesheet and can't fetch anything external — so the
    /// caller's CSS is inlined and the bands are wrapped in the same .pr-report root the
    /// document uses, keeping .pr-report-scoped rules working. Returns null when the report
    /// has no bands of that type.
    /// </summary>
    public async Task<string?> RenderBandsAsync(
        ResolvedReport report,
        BandType type,
        (double Top, double Right, double Bottom, double Left) margins,
        double bandsHeightMm,
        string? css = null,
        CancellationToken cancellationToken = default)
    {
        if (!report.Definition.Bands.Any(b => b.Type == type))
            return null;

        using var scope = scopeFactory.CreateScope();
        await using var renderer = new HtmlRenderer(scope.ServiceProvider, loggerFactory);

        var bands = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var parameters = ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                [nameof(PageBandHost.Resolved)] = report,
                [nameof(PageBandHost.Type)] = type,
                [nameof(PageBandHost.Mode)] = PageTokenMode.PdfTemplate,
            });
            var root = await renderer.RenderComponentAsync<PageBandHost>(parameters);
            return root.ToHtmlString();
        });

        // The template spans the whole paper width and the whole margin, so pad it in to
        // the content column and sit the bands against the content edge. A header fills the
        // margin and pads the page margin above itself; a footer takes only its own height
        // off the top, leaving the rest of the bottom margin for the page-number strip that
        // follows it and then the margin proper. Each side reads its own authored value —
        // one number for all three insets misplaced the header on an asymmetric margin.
        var left = PrintMargins.Mm(margins.Left);
        var right = PrintMargins.Mm(margins.Right);
        var box = type == BandType.PageHeader
            ? $"height:{PrintMargins.Mm(margins.Top + bandsHeightMm)};padding:{PrintMargins.Mm(margins.Top)} {right} 0 {left};"
            : $"height:{PrintMargins.Mm(bandsHeightMm)};padding:0 {right} 0 {left};";

        var html = new StringBuilder();
        if (!string.IsNullOrEmpty(css))
            html.Append("<style>").Append(css).Append("</style>");

        // 16px is the document's own base (WrapDocument sets no font-size), stated
        // explicitly because Chromium defaults template text to zero. It sits outside
        // .pr-report so a caller's own .pr-report font-size still wins.
        html.Append($"<div style=\"width:100%;box-sizing:border-box;font-size:16px;{box}\">");
        // Unlike every other StyleCss consumer here (which goes through Razor or HtmlRenderer,
        // both of which encode attribute values), this interpolates the CSS string raw into a
        // double-quoted attribute. Font stacks legitimately contain double quotes — e.g.
        // "Segoe UI", sans-serif — which would otherwise terminate the attribute early and
        // silently corrupt the PDF header/footer markup that follows.
        html.Append($"<div class=\"pr-report pr-print\" style=\"{System.Net.WebUtility.HtmlEncode(StyleCss.Inherited(report.Definition.Style))}\">");
        html.Append(bands);
        html.Append("</div></div>");
        return html.ToString();
    }

    private static string WrapDocument(ResolvedReport report, string body, string? css)
    {
        var page = report.Definition.Page;
        var html = new StringBuilder();
        html.Append("<!DOCTYPE html><html><head><meta charset=\"utf-8\"/>");
        html.Append($"<title>{System.Net.WebUtility.HtmlEncode(report.Definition.Name)}</title>");
        html.Append("<style>");
        // functional print setup only — page size from the definition and no default body offset
        html.Append($"@page {{ size: {page.Size}{(page.Landscape ? " landscape" : string.Empty)}; }}");
        html.Append("html, body { margin: 0; padding: 0; }");
        html.Append("</style>");
        if (!string.IsNullOrEmpty(css))
            html.Append("<style>").Append(css).Append("</style>");
        html.Append("</head><body>");
        html.Append(body);
        html.Append("</body></html>");
        return html.ToString();
    }
}
