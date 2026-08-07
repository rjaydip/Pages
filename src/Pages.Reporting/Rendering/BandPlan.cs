using Pages.Reporting.Core.Model;

namespace Pages.Reporting.Core.Rendering;

/// <summary>
/// One group a band is rendering inside: which column it partitions, the value of this
/// partition, and the rows belonging to it. <see cref="Parent"/> chains outward, so a
/// level-2 scope's rows are a subset of its level-1 parent's when both bind the same data set.
/// </summary>
/// <param name="DataSet">Name of the partitioned data set — a table only filters when its own binding names this one.</param>
/// <param name="Field">The column being partitioned on.</param>
/// <param name="Value">This partition's value of <paramref name="Field"/>, as it came from the data.</param>
/// <param name="Rows">The rows belonging to this partition, in the data set's own order.</param>
/// <param name="Parent">The enclosing group, or null at the outermost level.</param>
public sealed record GroupScope(
    string DataSet,
    string Field,
    object? Value,
    IReadOnlyList<IReadOnlyDictionary<string, object?>> Rows,
    GroupScope? Parent)
{
    /// <summary>
    /// The nearest scope in this chain that partitions <paramref name="dataSet"/>, or null when
    /// none does. Nested groups can bind different data sets, so the innermost scope is not
    /// always the relevant one — a table bound to the outer group's data set must still see the
    /// outer group's rows, not the whole set.
    /// </summary>
    public GroupScope? For(string dataSet) =>
        string.Equals(DataSet, dataSet, StringComparison.OrdinalIgnoreCase) ? this : Parent?.For(dataSet);
}

/// <summary>A band about to render, and the innermost group it sits in (null outside any group).</summary>
public sealed record BandPlanNode(PageBand Band, GroupScope? Scope);

/// <summary>
/// Expands <see cref="Report.Bands"/> into the sequence that actually renders: the bands
/// between a group header and its footer appear once per distinct value of the grouped
/// column, and every band carries the scope it belongs to.
///
/// This is the only place the expansion happens. The Blazor render path and the Excel
/// exporter both walk the result, which is what stops screen, PDF and sheet disagreeing.
/// </summary>
public static class BandPlan
{
    public static IReadOnlyList<BandPlanNode> Build(ResolvedReport report)
    {
        var bands = report.Definition.Bands
            .Where(band => !BandTypes.IsMargin(band.Type))
            .ToList();

        var nodes = new List<BandPlanNode>();
        Expand(report, bands, 0, bands.Count, scope: null, nodes);
        return nodes;
    }

    /// <summary>
    /// Walks [start, end) emitting nodes. On a group header it finds the matching footer,
    /// partitions the rows, and re-walks the span between them once per group — recursion is
    /// what gives nesting for free, because the inner walk sees the outer scope as its parent.
    /// </summary>
    private static void Expand(
        ResolvedReport report,
        List<PageBand> bands,
        int start,
        int end,
        GroupScope? scope,
        List<BandPlanNode> nodes)
    {
        for (var i = start; i < end; i++)
        {
            var band = bands[i];
            if (band.Type != BandType.GroupHeader)
            {
                // A footer with no header of its own reaches here. It prints once, in place,
                // in whatever scope surrounds it — a malformed report still has to print.
                nodes.Add(new BandPlanNode(band, scope));
                continue;
            }

            var footer = FindFooter(bands, i, end, band.Level);
            var groups = Partition(report, band, scope);
            if (groups.Count == 0)
            {
                // No binding, or a data set that produced nothing: the header still prints
                // once so the author sees the band exists rather than silently losing it. The
                // footer - 1 lets it also print once, ungrouped, on the generic branch's next
                // iteration. The whole enclosed span is skipped — only header and footer appear.
                nodes.Add(new BandPlanNode(band, scope));
                i = footer < 0 ? i : footer - 1;
                continue;
            }

            foreach (var group in groups)
            {
                nodes.Add(new BandPlanNode(band, group));
                if (footer < 0)
                    continue; // orphan header: it repeats, but nothing is enclosed by it
                Expand(report, bands, i + 1, footer, group, nodes);
                nodes.Add(new BandPlanNode(bands[footer], group));
            }

            // Skip the span just emitted; an orphan header encloses nothing.
            i = footer < 0 ? i : footer;
        }
    }

    /// <summary>
    /// The matching footer's index, or -1. Matching is by level, and a nested header of the
    /// same level would be malformed — so the first same-level footer wins.
    /// </summary>
    private static int FindFooter(List<PageBand> bands, int headerIndex, int end, int level)
    {
        for (var i = headerIndex + 1; i < end; i++)
            if (bands[i].Type == BandType.GroupFooter && bands[i].Level == level)
                return i;
        return -1;
    }

    /// <summary>
    /// Splits the header's data set into one scope per distinct value, in first-appearance
    /// order — the author controls group order with ORDER BY, and an implicit sort here would
    /// silently fight a query already ordered by date. Inside a parent scope only the
    /// parent's rows are partitioned.
    /// </summary>
    private static List<GroupScope> Partition(ResolvedReport report, PageBand header, GroupScope? parent)
    {
        // required string is a presence check, not a non-null one — STJ will happily bind
        // "dataSet": null — and the caller owns this JSON. A binding missing either half
        // partitions nothing, so the header prints once, ungrouped, exactly as the designer's
        // warning banner tells the author it will.
        if (header.Group is not { } binding
            || string.IsNullOrWhiteSpace(binding.DataSet)
            || string.IsNullOrWhiteSpace(binding.Field))
            return [];
        // An unknown name, a failed query and a genuinely empty result all mean the same thing
        // here — nothing to partition — but a failed query must not be mistaken for empty data.
        if (!report.DataSets.TryGetValue(binding.DataSet, out var data)
            || data.Error is not null
            || data.Table is null)
            return [];

        // A field naming no column would bucket every row under null: one meaningless group that
        // looks like it worked. Refusing to partition prints the band once, ungrouped, which is
        // at least honest.
        if (!data.Table.Columns.Contains(binding.Field, StringComparer.OrdinalIgnoreCase))
            return [];

        var rows = parent?.For(binding.DataSet)?.Rows ?? data.Table.Rows;

        var order = new List<object?>();
        var buckets = new Dictionary<string, List<IReadOnlyDictionary<string, object?>>>();
        foreach (var row in rows)
        {
            var value = row.GetValueOrDefault(binding.Field);
            var key = value?.ToString() ?? string.Empty;
            if (!buckets.TryGetValue(key, out var bucket))
            {
                buckets[key] = bucket = [];
                order.Add(value);
            }
            bucket.Add(row);
        }

        return [.. order.Select(value =>
            new GroupScope(binding.DataSet, binding.Field, value,
                buckets[value?.ToString() ?? string.Empty], parent))];
    }
}
