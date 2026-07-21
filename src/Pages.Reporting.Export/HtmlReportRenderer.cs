using System.Text;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Pages.Reporting.Blazor;
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
