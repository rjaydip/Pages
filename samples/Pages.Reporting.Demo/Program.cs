using Pages.Reporting.Core.DependencyInjection;
using Pages.Reporting.Core.Rendering;
using Pages.Reporting.Core.Serialization;
using Pages.Reporting.Demo.Components;
using Pages.Reporting.Demo.Data;
using Pages.Reporting.Export;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// The library: one call wires serialization, encryption, data resolution, and generation.
builder.Services.AddPagesReporting(options =>
    options.EncryptionKey = builder.Configuration["PagesReporting:EncryptionKey"]);
// The PDF gets the same report stylesheet the browser loads (see App.razor), so the
// download matches the on-screen view — without it Chromium prints in its serif default.
builder.Services.AddPagesReportingExport(options =>
    options.Css = File.ReadAllText(Path.Combine(builder.Environment.WebRootPath, "report.css")));

// Demo-side storage of report JSON (the "caller" role).
var dbPath = DemoDatabase.GetDbPath(builder.Environment);
var stagingDbPath = DemoDatabase.GetStagingDbPath(builder.Environment);
builder.Services.AddSingleton(new ReportRepository(dbPath));

var app = builder.Build();

DemoDatabase.Initialize(dbPath);
// A second database with different figures, so a report marked "supplied at runtime" has
// somewhere else to point — see EnvConnections below and docs/report-definition.md.
DemoDatabase.Initialize(stagingDbPath, seed: 7);

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Export endpoints: the stored JSON goes in, PDF/Excel bytes come out. Reader fill-in values
// come from the query string (?minRevenue=5000); host-supplied parameters (user, tenant) are
// set server-side and overlaid last so a reader cannot override them from the URL.
// ?env=staging points a report's runtime-supplied "sales" connection at the second database.
ReportRuntimeOptions RuntimeOptions(HttpRequest request)
{
    var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
    foreach (var (key, value) in request.Query)
        values[key] = value.ToString();

    // A real app reads the authenticated user here (HttpContext.User). The demo has no auth,
    // so it uses the server's OS user — do not copy this into production. Host-wins: these
    // overwrite anything a reader put in the query string.
    values["user"] = Environment.UserName;
    values["tenant"] = "acme";

    return new() { Parameters = values, ConnectionStrings = EnvConnections(request) };
}

// A fixed, server-side map — the connection string never comes from the client, only the
// environment name does. A real app would read these from configuration or a secret store.
Dictionary<string, string?>? EnvConnections(HttpRequest request) =>
    string.Equals(request.Query["env"], "staging", StringComparison.OrdinalIgnoreCase)
        ? new() { ["sales"] = $"Data Source={stagingDbPath}" }
        : null;

app.MapGet("/api/reports/{id:int}/pdf", async (int id, HttpRequest request, ReportRepository reports, PdfReportExporter exporter, CancellationToken ct) =>
{
    if (reports.Get(id) is not { } report)
        return Results.NotFound();
    byte[] pdf = await exporter.ExportAsync(report.Json, options: RuntimeOptions(request), cancellationToken: ct);
    return Results.File(pdf, "application/pdf", $"{report.Name}.pdf");
});

// Same PDF, but with no download filename so Content-Disposition is "inline" — the
// browser renders it in place. This is what the View Report page's <iframe> loads, so the
// on-screen report is the real paginated artifact (discrete pages, repeating bands, real
// page numbers) rather than the approximate single-sheet HTML view.
app.MapGet("/api/reports/{id:int}/pdf-inline", async (int id, HttpRequest request, ReportRepository reports, PdfReportExporter exporter, CancellationToken ct) =>
{
    if (reports.Get(id) is not { } report)
        return Results.NotFound();
    byte[] pdf = await exporter.ExportAsync(report.Json, options: RuntimeOptions(request), cancellationToken: ct);
    return Results.File(pdf, "application/pdf");
});

app.MapGet("/api/reports/{id:int}/xlsx", async (int id, HttpRequest request, ReportRepository reports, ExcelReportExporter exporter, CancellationToken ct) =>
{
    if (reports.Get(id) is not { } report)
        return Results.NotFound();
    byte[] workbook = await exporter.ExportAsync(report.Json, RuntimeOptions(request), ct);
    return Results.File(workbook, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"{report.Name}.xlsx");
});

app.Run();
