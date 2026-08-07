using Pages.Reporting.Core.Model;

namespace Pages.Reporting.Core.Rendering;

/// <summary>
/// Re-renders a text element's template against one group's rows. The resolver renders text
/// once, at resolve time, which cannot work for an element that renders once per group with a
/// different value each time — so inside a scope the same tokenizer runs again here, over the
/// scope's row subset instead of the whole data set.
/// </summary>
public static class ScopedValues
{
    /// <summary>The data set name that reads the current group rather than a query result.</summary>
    internal const string GroupDataSet = "group";

    public static string Render(ResolvedReport report, GroupScope scope, string? content)
    {
        var result = new System.Text.StringBuilder();
        foreach (var token in TextTemplate.Tokenize(content))
        {
            if (token.Expression is not { } expression)
            {
                result.Append(token.Raw);
                continue;
            }

            if (expression.Builtin is not BuiltinToken.None)
            {
                result.Append(expression.Builtin switch
                {
                    BuiltinToken.ReportName => report.Definition.Name,
                    BuiltinToken.Now => ValueFormatter.Format(DateTime.Now, expression.Format),
                    // Only the printer knows these — defer via a placeholder (see PageTokens).
                    BuiltinToken.Page => PageTokens.PagePlaceholder.ToString(),
                    BuiltinToken.Pages => PageTokens.PagesPlaceholder.ToString(),
                    _ => string.Empty,
                });
                continue;
            }

            // {@param} needs the same map available at render time, matching the resolver's behaviour.
            if (expression.Parameter is { } parameterName)
            {
                result.Append(report.Parameters.TryGetValue(parameterName, out var value)
                    ? value
                    : $"⚠ unknown parameter '{parameterName}'");
                continue;
            }

            // {group.Field} — the value of the enclosing group, walking outwards so a nested
            // band can still name an outer group's column.
            if (string.Equals(expression.DataSet, GroupDataSet, StringComparison.OrdinalIgnoreCase))
            {
                if (TryFindGroupValue(scope, expression.Column, out var groupValue))
                {
                    result.Append(ValueFormatter.Format(groupValue, expression.Format));
                }
                else
                {
                    // Showing the innermost group's value would be a wrong number under the very
                    // name the author asked for, so signal the error instead.
                    result.Append($"⚠ no group field '{expression.Column}'");
                }
                continue;
            }

            if (!report.DataSets.TryGetValue(expression.DataSet!, out var data))
            {
                // This matches GetDataSetAsync's error for an unknown name.
                result.Append($"⚠ The report declares no data set named '{expression.DataSet}'.");
                continue;
            }

            if (data.Error is not null)
            {
                result.Append($"⚠ {data.Error}");
                continue;
            }

            if (data.Table is null)
            {
                // Silently skip if there is no table and no error (shouldn't happen, but be safe).
                continue;
            }

            // Walks outward: a token naming an OUTER group's data set gets that group's rows.
            var rows = scope.For(expression.DataSet!)?.Rows ?? data.Table.Rows;

            result.Append(ValueFormatter.Format(
                ScalarValues.Extract(rows, expression.Column, expression.Aggregate), expression.Format));
        }

        return result.ToString();
    }

    /// <summary>
    /// The value of the nearest enclosing group whose column matches. A bare <c>{group.}</c>
    /// means "whichever group this is", so the innermost scope answers it; a column that names
    /// no enclosing group is an authoring error and is reported as one by the caller.
    /// </summary>
    private static bool TryFindGroupValue(GroupScope scope, string? column, out object? value)
    {
        // Bare {group.} means "this group's value", so the innermost scope answers it.
        if (string.IsNullOrWhiteSpace(column))
        {
            value = scope.Value;
            return true;
        }

        // Named column: walk the Parent chain for a match.
        for (var current = scope; current is not null; current = current.Parent)
            if (string.Equals(current.Field, column, StringComparison.OrdinalIgnoreCase))
            {
                value = current.Value;
                return true;
            }

        // No match in the scope chain: error.
        value = null;
        return false;
    }
}
