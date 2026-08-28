using Pages.Reporting.Core.Model;

namespace Pages.Reporting.Core.Rendering;

/// <summary>A report definition paired with the data fetched for each of its elements.</summary>
public sealed class ResolvedReport
{
    private readonly Dictionary<ReportElement, ResolvedData> _data;

    public ResolvedReport(
        Report definition,
        Dictionary<ReportElement, ResolvedData> data,
        IReadOnlyList<ConnectionFailure>? connectionFailures = null,
        IReadOnlyDictionary<string, ResolvedData>? dataSets = null,
        IReadOnlyDictionary<string, string?>? parameters = null)
    {
        Definition = definition;
        _data = data;
        ConnectionFailures = connectionFailures ?? [];
        DataSets = dataSets ?? new Dictionary<string, ResolvedData>(StringComparer.OrdinalIgnoreCase);
        Parameters = parameters ?? new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
    }

    public Report Definition { get; }

    /// <summary>
    /// Connections that could not be opened during this resolution. Every element bound to
    /// one of them also carries the same message, but a report-level banner is the only way
    /// the reader sees "the database is unreachable" rather than a grid of "unavailable".
    /// </summary>
    public IReadOnlyList<ConnectionFailure> ConnectionFailures { get; }

    /// <summary>
    /// Every data set's result, by name. Element data is keyed by element and resolved once;
    /// grouping instead partitions a whole data set at render time, so these have to outlive
    /// the resolution that produced them.
    /// </summary>
    public IReadOnlyDictionary<string, ResolvedData> DataSets { get; }

    /// <summary>
    /// The effective parameter values this resolution ran with. Text is normally substituted
    /// once, at resolve time, but a text element inside a group band is re-rendered per group
    /// — so {@param} needs the same map available again at render time.
    /// </summary>
    public IReadOnlyDictionary<string, string?> Parameters { get; }

    public ResolvedData GetData(ReportElement element) =>
        _data.TryGetValue(element, out var data) ? data : ResolvedData.Empty;
}

/// <summary>A named connection that failed to open, and why.</summary>
/// <param name="Connection">The connection name as declared in the report.</param>
/// <param name="Message">
/// A safe, reader-facing summary — the connection name, not the provider's message, which can
/// carry the connection string. The provider detail is logged server-side instead.
/// </param>
public sealed record ConnectionFailure(string Connection, string Message);

/// <summary>Data fetched for one element: a scalar, a table, or a per-element error.</summary>
public sealed class ResolvedData
{
    public static readonly ResolvedData Empty = new();

    public object? Scalar { get; init; }

    public ReportDataTable? Table { get; init; }

    /// <summary>Set when this element's query failed; the rest of the report still renders.</summary>
    public string? Error { get; init; }
}

/// <summary>Provider-agnostic result set.</summary>
public sealed class ReportDataTable
{
    public ReportDataTable(IReadOnlyList<string> columns, IReadOnlyList<IReadOnlyDictionary<string, object?>> rows)
    {
        Columns = columns;
        Rows = rows;
    }

    public IReadOnlyList<string> Columns { get; }

    public IReadOnlyList<IReadOnlyDictionary<string, object?>> Rows { get; }
}
