using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Pages.Reporting.Core.Rendering;
using Pages.Reporting.Core.Security;
using Pages.Reporting.Core.Serialization;

namespace Pages.Reporting.Core.DependencyInjection;

public sealed class PagesReportingOptions
{
    /// <summary>
    /// Encryption key for connection strings: base64 of 32 bytes, or any passphrase.
    /// Falls back to the PAGES_REPORTING_KEY environment variable, then to an
    /// auto-generated key file (pages-reporting.key) next to the application.
    /// </summary>
    public string? EncryptionKey { get; set; }

    /// <summary>Override the auto-generated key file location.</summary>
    public string? KeyFilePath { get; set; }
}

public static class ServiceCollectionExtensions
{
    /// <summary>Registers everything needed to serialize, generate, and render reports.</summary>
    public static IServiceCollection AddPagesReporting(
        this IServiceCollection services,
        Action<PagesReportingOptions>? configure = null)
    {
        var options = new PagesReportingOptions();
        configure?.Invoke(options);

        services.TryAddSingleton<IReportCipher>(_ =>
            new AesGcmReportCipher(ReportCipherKey.Resolve(options.EncryptionKey, options.KeyFilePath)));
        services.TryAddSingleton<ReportJson>();
        services.TryAddSingleton<ReportDataResolver>();
        services.TryAddSingleton<IReportGenerator, ReportGenerator>();

        return services;
    }
}
