using Pages.Reporting.Core.Model;

namespace Pages.Reporting.Core.Rendering;

/// <summary>
/// Entry point for report generation: give it the report JSON the caller stored
/// (or an in-memory <see cref="Report"/>) and get back a <see cref="ResolvedReport"/>
/// ready for on-screen rendering or export. Optional <c>parameters</c> override the
/// report's declared parameter defaults (e.g. values taken from the page URL).
/// </summary>
public interface IReportGenerator
{
    Task<ResolvedReport> GenerateAsync(
        string reportJson,
        IReadOnlyDictionary<string, string?>? parameters = null,
        CancellationToken cancellationToken = default);

    Task<ResolvedReport> GenerateAsync(
        Report report,
        IReadOnlyDictionary<string, string?>? parameters = null,
        CancellationToken cancellationToken = default);
}
