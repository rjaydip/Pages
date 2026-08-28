using Pages.Reporting.Core.Model;

namespace Pages.Reporting.Core.Rendering;

/// <summary>
/// Entry point for report generation: give it the report JSON the caller stored
/// (or an in-memory <see cref="Report"/>) and get back a <see cref="ResolvedReport"/>
/// ready for on-screen rendering or export. The optional <see cref="ReportRuntimeOptions"/>
/// carries the values supplied at generation time — parameter overrides and runtime
/// connection strings.
/// </summary>
public interface IReportGenerator
{
    /// <param name="reportJson">The report definition as produced by <c>ReportJson.Serialize</c>.</param>
    /// <param name="options">Parameter overrides and runtime connection strings; null applies every declared default.</param>
    /// <param name="cancellationToken">Cancels the data resolution.</param>
    Task<ResolvedReport> GenerateAsync(
        string reportJson,
        ReportRuntimeOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <param name="report">An in-memory report definition.</param>
    /// <param name="options">Parameter overrides and runtime connection strings; null applies every declared default.</param>
    /// <param name="cancellationToken">Cancels the data resolution.</param>
    Task<ResolvedReport> GenerateAsync(
        Report report,
        ReportRuntimeOptions? options = null,
        CancellationToken cancellationToken = default);
}
