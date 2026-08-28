using Pages.Reporting.Core.Model;
using Pages.Reporting.Core.Serialization;

namespace Pages.Reporting.Core.Rendering;

public sealed class ReportGenerator(ReportJson json, ReportDataResolver resolver) : IReportGenerator
{
    public Task<ResolvedReport> GenerateAsync(
        string reportJson,
        ReportRuntimeOptions? options = null,
        CancellationToken cancellationToken = default) =>
        GenerateAsync(json.Deserialize(reportJson), options, cancellationToken);

    public Task<ResolvedReport> GenerateAsync(
        Report report,
        ReportRuntimeOptions? options = null,
        CancellationToken cancellationToken = default) =>
        resolver.ResolveAsync(report, options, cancellationToken);
}
