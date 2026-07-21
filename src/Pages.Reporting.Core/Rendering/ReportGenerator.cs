using Pages.Reporting.Core.Model;
using Pages.Reporting.Core.Serialization;

namespace Pages.Reporting.Core.Rendering;

public sealed class ReportGenerator(ReportJson json, ReportDataResolver resolver) : IReportGenerator
{
    public Task<ResolvedReport> GenerateAsync(
        string reportJson,
        IReadOnlyDictionary<string, string?>? parameters = null,
        CancellationToken cancellationToken = default) =>
        GenerateAsync(json.Deserialize(reportJson), parameters, cancellationToken);

    public Task<ResolvedReport> GenerateAsync(
        Report report,
        IReadOnlyDictionary<string, string?>? parameters = null,
        CancellationToken cancellationToken = default) =>
        resolver.ResolveAsync(report, parameters, cancellationToken);
}
