using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Pages.Reporting.Export;

public sealed class ReportExportOptions
{
    /// <summary>
    /// CSS inlined into exported HTML/PDF. The library ships no styling — pass the same
    /// stylesheet your app uses on screen to make the PDF match it. Can be overridden
    /// per call via the exporter's css parameter.
    /// </summary>
    public string? Css { get; set; }
}

public static class ServiceCollectionExtensions
{
    /// <summary>Registers the PDF and Excel exporters. Call after AddPagesReporting().</summary>
    public static IServiceCollection AddPagesReportingExport(
        this IServiceCollection services,
        Action<ReportExportOptions>? configure = null)
    {
        var options = new ReportExportOptions();
        configure?.Invoke(options);

        services.TryAddSingleton(options);
        services.TryAddSingleton<HtmlReportRenderer>();
        services.TryAddSingleton<PdfReportExporter>();
        services.TryAddSingleton<ExcelReportExporter>();
        return services;
    }
}
