using Pages.Reporting.Core.Model;

namespace Pages.Reporting.Core.Rendering;

/// <summary>A report definition paired with the data fetched for each of its elements.</summary>
public sealed class ResolvedReport
{
    private readonly Dictionary<ReportElement, ResolvedData> _data;

    public ResolvedReport(Report definition, Dictionary<ReportElement, ResolvedData> data)
    {
        Definition = definition;
        _data = data;
    }

    public Report Definition { get; }

    public ResolvedData GetData(ReportElement element) =>
        _data.TryGetValue(element, out var data) ? data : ResolvedData.Empty;
}

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
