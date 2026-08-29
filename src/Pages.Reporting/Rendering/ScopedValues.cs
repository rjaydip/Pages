using Pages.Reporting.Core.Data;

namespace Pages.Reporting.Core.Rendering;

/// <summary>
/// Re-renders a text element's template against one group's rows. The resolver renders text
/// once, at resolve time, which cannot work for an element that renders once per group with a
/// different value each time — so inside a scope the same <see cref="TextRenderer"/> runs
/// again here, over the scope's row subset instead of the whole data set.
/// </summary>
public static class ScopedValues
{
    /// <summary>The data set name that reads the current group rather than a query result.</summary>
    internal const string GroupDataSet = "group";

    /// <param name="report">The resolved report (its data-set cache, parameters and variables).</param>
    /// <param name="scope">The group being rendered — column reads see its row subset.</param>
    /// <param name="content">The text element's raw template.</param>
    /// <param name="htmlContext">
    /// True when <paramref name="content"/> belongs to a <c>renderHtml</c> text element — see
    /// <see cref="TextRenderer.Render"/>.
    /// </param>
    public static string Render(ResolvedReport report, GroupScope scope, string? content, bool htmlContext) =>
        TextRenderer.Render(
            TextTemplate.Tokenize(content),
            new RenderTimeScope(report, scope),
            htmlContext);

    /// <summary>
    /// <see cref="IValueScope"/> over a resolved report and the group being rendered: column
    /// reads see the group's row subset, and <c>{group.x}</c> walks the scope chain.
    /// </summary>
    private sealed class RenderTimeScope(ResolvedReport report, GroupScope scope) : IValueScope
    {
        public bool TryParameter(string name, out string? value) =>
            report.Parameters.TryGetValue(name, out value);

        public ColumnResult Column(string dataSet, string? column, ScalarAggregate aggregate)
        {
            if (!report.DataSets.TryGetValue(dataSet, out var data))
                return ColumnResult.UnknownDataSet;
            if (data.Error is not null)
                return ColumnResult.Failed(data.Error);
            if (data.Table is null)
                return ColumnResult.Found(null);

            // Walks outward: a token naming an OUTER group's data set gets that group's rows.
            var rows = scope.For(dataSet)?.Rows ?? data.Table.Rows;
            return ColumnResult.Found(ScalarValues.Extract(rows, column, aggregate));
        }

        public GroupStatus Group(string? column, out object? value)
        {
            // Bare {group.} means "this group's value", so the innermost scope answers it.
            if (string.IsNullOrWhiteSpace(column))
            {
                value = scope.Value;
                return GroupStatus.Ok;
            }

            // Named column: walk the Parent chain for a match.
            for (var current = scope; current is not null; current = current.Parent)
                if (string.Equals(current.Field, column, StringComparison.OrdinalIgnoreCase))
                {
                    value = current.Value;
                    return GroupStatus.Ok;
                }

            value = null;
            return GroupStatus.NoSuchField;
        }

        public string ReportName => report.Definition.Name;

        public DateTime Now => report.GeneratedAt;
    }
}
