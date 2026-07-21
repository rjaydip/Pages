using Pages.Reporting.Core.DependencyInjection;
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
var dbPath = Path.Combine(builder.Environment.ContentRootPath, "demo.db");
builder.Services.AddSingleton(new ReportRepository(dbPath));

var app = builder.Build();

DemoDatabase.Initialize(dbPath, app.Services.GetRequiredService<ReportJson>());

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

// Export endpoints: the stored JSON goes in, PDF/Excel bytes come out. Request query
// values (?minRevenue=5000) override the report's parameter defaults.
static Dictionary<string, string?>? QueryParameters(HttpRequest request) =>
    request.Query.Count == 0
        ? null
        : request.Query.ToDictionary(kv => kv.Key, kv => (string?)kv.Value.ToString());

app.MapGet("/api/reports/{id:int}/pdf", async (int id, HttpRequest request, ReportRepository reports, PdfReportExporter exporter, CancellationToken ct) =>
{
    if (reports.Get(id) is not { } report)
        return Results.NotFound();
    byte[] pdf = await exporter.ExportAsync(report.Json, parameters: QueryParameters(request), cancellationToken: ct);
    return Results.File(pdf, "application/pdf", $"{report.Name}.pdf");
});

app.MapGet("/api/reports/{id:int}/xlsx", async (int id, HttpRequest request, ReportRepository reports, ExcelReportExporter exporter, CancellationToken ct) =>
{
    if (reports.Get(id) is not { } report)
        return Results.NotFound();
    byte[] workbook = await exporter.ExportAsync(report.Json, QueryParameters(request), ct);
    return Results.File(workbook, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"{report.Name}.xlsx");
});

app.Run();
