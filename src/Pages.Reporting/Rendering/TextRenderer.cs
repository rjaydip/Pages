using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Pages.Reporting.Core.Rendering;

/// <summary>
/// Turns a tokenized text template into its final string for one render pass. This is the
/// single place a <c>{…}</c> token becomes text: the resolver (resolve time, whole data set)
/// and <see cref="ScopedValues"/> (render time, one group) both call it through an
/// <see cref="IValueScope"/>, so the two paths cannot diverge.
/// </summary>
internal static class TextRenderer
{
    /// <param name="tokens">The tokenized template.</param>
    /// <param name="scope">Supplies parameter, data-set and group values.</param>
    /// <param name="htmlContext">
    /// True when the result is emitted as raw markup (a <c>renderHtml</c> text element).
    /// <c>{@param}</c> and <c>{reportName}</c> values are then HTML-escaped so a host- or
    /// reader-supplied value cannot inject tags; literal runs (the author's own markup) and
    /// <c>{dataSet.Column}</c> values are left as-is.
    /// </param>
    public static string Render(
        IReadOnlyList<TextTemplate.TextToken> tokens, IValueScope scope, bool htmlContext)
    {
        var result = new StringBuilder();
        foreach (var token in tokens)
        {
            switch (token.Expression)
            {
                case null:
                    result.Append(token.Raw);
                    break;

                case TextTemplate.BuiltinExpr builtin:
                    result.Append(RenderBuiltin(builtin, scope, htmlContext));
                    break;

                case TextTemplate.ParameterExpr parameter:
                    result.Append(scope.TryParameter(parameter.Name, out var parameterValue)
                        ? Encode(parameterValue, htmlContext)
                        : $"⚠ unknown parameter '{parameter.Name}'");
                    break;

                // {group.Column} — the enclosing group. Outside a group it renders nothing:
                // claiming the report is missing a data set called "group" would be false and,
                // on an empty result set, the only thing the reader sees.
                case TextTemplate.DataSetExpr group
                    when string.Equals(group.DataSet, ScopedValues.GroupDataSet, StringComparison.OrdinalIgnoreCase):
                    switch (scope.Group(group.Column, out var groupValue))
                    {
                        case GroupStatus.Ok:
                            result.Append(ValueFormatter.Format(groupValue, group.Format));
                            break;
                        case GroupStatus.NoSuchField:
                            // Showing the innermost group's value under a name the author did
                            // not ask for would be a wrong number; signal the error instead.
                            result.Append($"⚠ no group field '{group.Column}'");
                            break;
                        case GroupStatus.NotInGroup:
                            break;
                    }
                    break;

                case TextTemplate.DataSetExpr dataSet:
                    var column = scope.Column(dataSet.DataSet, dataSet.Column, dataSet.Aggregate);
                    switch (column.Status)
                    {
                        case ColumnStatus.Ok:
                            result.Append(ValueFormatter.Format(column.Value, dataSet.Format));
                            break;
                        case ColumnStatus.UnknownDataSet:
                            result.Append($"⚠ The report declares no data set named '{dataSet.DataSet}'.");
                            break;
                        case ColumnStatus.DataSetError:
                            result.Append($"⚠ {column.Error}");
                            break;
                    }
                    break;

                default:
                    result.Append(token.Raw);
                    break;
            }
        }

        return result.ToString();
    }

    private static string RenderBuiltin(TextTemplate.BuiltinExpr builtin, IValueScope scope, bool htmlContext) =>
        builtin.Token switch
        {
            BuiltinToken.ReportName => Encode(scope.ReportName, htmlContext),
            BuiltinToken.Now => ValueFormatter.Format(scope.Now, builtin.Format),
            // Only the printer knows these — defer via a placeholder (see PageTokens).
            BuiltinToken.Page => PageTokens.PagePlaceholder.ToString(),
            BuiltinToken.Pages => PageTokens.PagesPlaceholder.ToString(),
            _ => string.Empty,
        };

    /// <summary>
    /// HTML-escapes the five markup-significant characters when a token's value is going into
    /// a <c>renderHtml</c> element as raw markup — a parameter or report-name value is data,
    /// not markup, and must not be able to inject tags. Deliberately minimal (not a full
    /// encoder) so a value with no such characters is byte-identical to before, and accented
    /// letters and other non-ASCII text pass through unchanged.
    /// </summary>
    [return: NotNullIfNotNull(nameof(value))]
    private static string? Encode(string? value, bool htmlContext)
    {
        if (!htmlContext || string.IsNullOrEmpty(value)
            || value.IndexOfAny(['&', '<', '>', '"', '\'']) < 0)
            return value;

        return value
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&#39;");
    }
}
