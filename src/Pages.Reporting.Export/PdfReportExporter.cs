using Microsoft.Playwright;
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
        var html = await htmlRenderer.RenderAsync(report, css ?? options.Css, cancellationToken);
        var browser = await GetBrowserAsync();

        var page = await browser.NewPageAsync();
        try
        {
            await page.SetContentAsync(html, new PageSetContentOptions { WaitUntil = WaitUntilState.Load });

            var settings = report.Definition.Page;
            // Only the print margin — the page style's border/padding live inside it,
            // on the report root, cloned per page (see StyleCss.PageContent).
            var margin = PrintMargins.ComputeMm(settings.Margin);
            return await page.PdfAsync(new PagePdfOptions
            {
                Format = settings.Size,
                Landscape = settings.Landscape,
                PrintBackground = true,
                Margin = new Margin
                {
                    Top = PrintMargins.Mm(margin),
                    // The footer needs extra room below the content.
                    Bottom = PrintMargins.Mm(settings.ShowFooter ? margin + Core.Model.PageSettings.FooterReserveMm : margin),
                    Left = PrintMargins.Mm(margin),
                    Right = PrintMargins.Mm(margin),
                },
                DisplayHeaderFooter = settings.ShowFooter,
                HeaderTemplate = "<span></span>",
                FooterTemplate = settings.ShowFooter
                    ? "<div style=\"width:100%;font-size:9px;color:#898781;padding:0 15mm;" +
                      "display:flex;justify-content:space-between;font-family:system-ui,sans-serif;\">" +
                      $"<span>{System.Net.WebUtility.HtmlEncode(report.Definition.Name)}</span>" +
                      "<span><span class=\"pageNumber\"></span> / <span class=\"totalPages\"></span></span></div>"
                    : "<span></span>",
            });
        }
        finally
        {
            await page.CloseAsync();
        }
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
