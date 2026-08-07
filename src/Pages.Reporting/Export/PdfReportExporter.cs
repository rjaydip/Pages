using Microsoft.Playwright;
using Pages.Reporting.Blazor;
using Pages.Reporting.Core.Model;
using Pages.Reporting.Core.Rendering;

namespace Pages.Reporting.Export;

/// <summary>
/// Converts a report to PDF bytes by printing the exported HTML with headless Chromium.
/// Because the HTML uses the same components and CSS as the on-screen view, the PDF
/// looks the same as the report in the browser.
/// </summary>
public sealed class PdfReportExporter(
    HtmlReportRenderer htmlRenderer,
    IReportGenerator generator,
    ReportExportOptions options) : IAsyncDisposable
{
    private readonly SemaphoreSlim _browserLock = new(1, 1);
    private IPlaywright? _playwright;
    private IBrowser? _browser;

    /// <summary>
    /// Generates the report from its stored JSON and returns the PDF as bytes.
    /// <paramref name="css"/> overrides the CSS configured in <see cref="ReportExportOptions"/>;
    /// <paramref name="parameters"/> overrides the report's parameter defaults.
    /// </summary>
    public async Task<byte[]> ExportAsync(
        string reportJson,
        string? css = null,
        IReadOnlyDictionary<string, string?>? parameters = null,
        CancellationToken cancellationToken = default) =>
        await ExportAsync(await generator.GenerateAsync(reportJson, parameters, cancellationToken), css, cancellationToken);

    public async Task<byte[]> ExportAsync(ResolvedReport report, string? css = null, CancellationToken cancellationToken = default)
    {
        var effectiveCss = css ?? options.Css;
        var html = await htmlRenderer.RenderAsync(report, effectiveCss, cancellationToken);
        var browser = await GetBrowserAsync();

        var page = await browser.NewPageAsync();
        try
        {
            await page.SetContentAsync(html, new PageSetContentOptions { WaitUntil = WaitUntilState.Load });

            var settings = report.Definition.Page;
            // Only the print margin — the page style's border/padding live inside it,
            // on the report root, cloned per page (see StyleCss.PageContent).
            var margins = PageSheet.MarginsMm(settings);

            // PageHeader/PageFooter bands print in the margin, so the margin has to grow by
            // their height before Chromium paginates — same idea as the footer strip's reserve.
            var headerBands = BandReserveMm(report, BandType.PageHeader);
            var footerBands = BandReserveMm(report, BandType.PageFooter);

            var headerTemplate = await htmlRenderer.RenderBandsAsync(
                report, BandType.PageHeader, margins, headerBands, effectiveCss, cancellationToken);
            var bandFooter = await htmlRenderer.RenderBandsAsync(
                report, BandType.PageFooter, margins, footerBands, effectiveCss, cancellationToken);

            var top = margins.Top + headerBands;
            var bottom = margins.Bottom + footerBands
                + (settings.ShowFooter ? Core.Model.PageSettings.FooterReserveMm : 0);

            await PinLastPageFooterAsync(page, report, settings, top, bottom);

            var footerTemplate = string.Concat(
                bandFooter,
                settings.ShowFooter ? PageNumberStrip(report, margins) : null);

            return await page.PdfAsync(new PagePdfOptions
            {
                Format = settings.Size,
                Landscape = settings.Landscape,
                PrintBackground = true,
                Margin = new Margin
                {
                    Top = PrintMargins.Mm(top),
                    Bottom = PrintMargins.Mm(bottom),
                    Left = PrintMargins.Mm(margins.Left),
                    Right = PrintMargins.Mm(margins.Right),
                },
                DisplayHeaderFooter = headerTemplate is not null || footerTemplate.Length > 0,
                // Chromium renders its own default template unless given something.
                HeaderTemplate = headerTemplate ?? "<span></span>",
                FooterTemplate = footerTemplate.Length > 0 ? footerTemplate : "<span></span>",
            });
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    private static double BandReserveMm(ResolvedReport report, BandType type) =>
        report.Definition.Bands
            .Where(band => band.Type == type)
            .Sum(band => band.HeightMm);

    private static string PageNumberStrip(ResolvedReport report, (double Top, double Right, double Bottom, double Left) margins)
    {
        var left = PrintMargins.Mm(margins.Left);
        var right = PrintMargins.Mm(margins.Right);
        return "<div style=\"width:100%;box-sizing:border-box;font-size:9px;color:#898781;" +
               $"padding:0 {right} 0 {left};display:flex;justify-content:space-between;" +
               "font-family:system-ui,sans-serif;\">" +
               $"<span>{System.Net.WebUtility.HtmlEncode(report.Definition.Name)}</span>" +
               "<span><span class=\"pageNumber\"></span> / <span class=\"totalPages\"></span></span></div>";
    }

    /// <summary>
    /// A report summary that is genuinely the last band flows after the content, so it lands
    /// wherever the content happens to end. CSS can't reach the bottom of the *last* page, so
    /// grow the report root to a whole number of pages first — its margin-top:auto pin then
    /// sits on the last page's bottom edge. List order is the truth (see
    /// <see cref="BandTypes.PinsToBottom"/>): a summary moved off the end just prints in
    /// place, so this only grows the root when one is genuinely pinned.
    /// </summary>
    private static async Task PinLastPageFooterAsync(
        IPage page, ResolvedReport report, Core.Model.PageSettings settings, double topMm, double bottomMm)
    {
        if (!BandTypes.HasPinnedSummary(report.Definition))
            return;

        var (_, paperHeight) = Blazor.PageSheet.SizeMm(settings);
        var contentHeightPx = (paperHeight - topMm - bottomMm) * 96 / 25.4;
        if (contentHeightPx <= 0)
            return;

        await page.EvaluateAsync(
            """
            pageHeight => {
                const root = document.querySelector('.pr-report');
                if (!root) return;
                root.style.minHeight = '0px';
                // 2px of slack: a content height a hair over a page boundary (sub-pixel
                // layout rounding) must not reserve an extra, empty page.
                const pages = Math.max(1, Math.ceil((root.scrollHeight - 2) / pageHeight));
                root.style.minHeight = (pages * pageHeight) + 'px';
            }
            """,
            contentHeightPx);
    }

    private async Task<IBrowser> GetBrowserAsync()
    {
        if (_browser is not null)
            return _browser;

        await _browserLock.WaitAsync();
        try
        {
            if (_browser is not null)
                return _browser;

            _playwright ??= await Playwright.CreateAsync();
            try
            {
                _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
            }
            catch (PlaywrightException)
            {
                // Chromium not installed yet — download it once, then retry.
                await Task.Run(() => Program.Main(["install", "chromium"]));
                _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
            }
            return _browser;
        }
        finally
        {
            _browserLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_browser is not null)
            await _browser.DisposeAsync();
        _playwright?.Dispose();
        _browserLock.Dispose();
    }
}
