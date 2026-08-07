using Pages.Reporting.Core.Data;

namespace Pages.Reporting.Core.Rendering;

/// <summary>
/// Tokenizer for <c>TextElement</c> content. Splits literal runs from
/// <c>{dataSet.Column:aggregate:format}</c> / <c>{@param}</c> expressions;
/// <c>{{</c> escapes a literal <c>{</c>. Shared by the resolver (real values)
/// and the designer skeleton (ghost pills).
/// </summary>
public static class TextTemplate
{
    /// <summary>A literal run (<see cref="Expression"/> null) or a parsed expression.</summary>
    public sealed record TextToken(string Raw, TextExpression? Expression);

    /// <summary>
    /// A data set read (DataSet+Column), a parameter reference (Parameter), or a
    /// built-in value (Builtin).
    /// </summary>
    public sealed record TextExpression(
        string? DataSet,
        string? Column,
        ScalarAggregate Aggregate,
        string? Format,
        string? Parameter,
        BuiltinToken Builtin = BuiltinToken.None);

    public static IReadOnlyList<TextToken> Tokenize(string? content)
    {
        var tokens = new List<TextToken>();
        if (string.IsNullOrEmpty(content))
            return tokens;

        var literal = new System.Text.StringBuilder();
        var i = 0;
        while (i < content.Length)
        {
            var c = content[i];
            if (c == '{')
            {
                if (i + 1 < content.Length && content[i + 1] == '{')
                {
                    literal.Append('{');
                    i += 2;
                    continue;
                }

                var close = content.IndexOf('}', i + 1);
                if (close < 0)
                {
                    literal.Append(content, i, content.Length - i);
                    break;
                }

                if (literal.Length > 0)
                {
                    tokens.Add(new TextToken(literal.ToString(), null));
                    literal.Clear();
                }

                var raw = content.Substring(i, close - i + 1);
                tokens.Add(new TextToken(raw, ParseExpression(content.Substring(i + 1, close - i - 1))));
                i = close + 1;
                continue;
            }

            literal.Append(c);
            i++;
        }

        if (literal.Length > 0)
            tokens.Add(new TextToken(literal.ToString(), null));
        return tokens;
    }

    private static TextExpression? ParseExpression(string inner)
    {
        inner = inner.Trim();
        if (inner.Length == 0)
            return null;

        if (inner.StartsWith('@'))
        {
            var parameter = inner[1..].Trim();
            return parameter.Length == 0 ? null : new TextExpression(null, null, ScalarAggregate.First, null, parameter);
        }

        // Built-ins are bare names, optionally with a format: {page}, {now:yyyy-MM-dd}.
        // Bare names never matched anything before (they need a '.'), so this is additive.
        var builtinName = inner;
        string? builtinFormat = null;
        var colon = inner.IndexOf(':');
        if (colon > 0)
        {
            builtinName = inner[..colon].Trim();
            builtinFormat = inner[(colon + 1)..];
        }
        if (TryParseBuiltin(builtinName, out var builtin))
            return new TextExpression(null, null, ScalarAggregate.First, builtinFormat, null, builtin);

        var dot = inner.IndexOf('.');
        if (dot <= 0 || dot == inner.Length - 1)
            return null;

        var dataSet = inner[..dot].Trim();
        var rest = inner[(dot + 1)..];

        // rest = Column[:aggregate][:format] — format may itself contain ':' (e.g. HH:mm).
        var parts = rest.Split(':');
        var column = parts[0].Trim();
        if (dataSet.Length == 0 || column.Length == 0)
            return null;

        var aggregate = ScalarAggregate.First;
        string? format = null;
        if (parts.Length > 1)
        {
            var next = 1;
            if (TryParseAggregate(parts[1].Trim(), out var parsed))
            {
                aggregate = parsed;
                next = 2;
            }
            if (parts.Length > next)
                format = string.Join(':', parts[next..]);
        }

        return new TextExpression(dataSet, column, aggregate, format, null);
    }

    private static bool TryParseBuiltin(string value, out BuiltinToken builtin)
    {
        switch (value.ToLowerInvariant())
        {
            case "page": builtin = BuiltinToken.Page; return true;
            case "pages": builtin = BuiltinToken.Pages; return true;
            case "reportname": builtin = BuiltinToken.ReportName; return true;
            case "now": builtin = BuiltinToken.Now; return true;
            default: builtin = BuiltinToken.None; return false;
        }
    }

    private static bool TryParseAggregate(string value, out ScalarAggregate aggregate)
    {
        switch (value.ToLowerInvariant())
        {
            case "first": aggregate = ScalarAggregate.First; return true;
            case "sum": aggregate = ScalarAggregate.Sum; return true;
            case "avg":
            case "average": aggregate = ScalarAggregate.Average; return true;
            case "count": aggregate = ScalarAggregate.Count; return true;
            case "min": aggregate = ScalarAggregate.Min; return true;
            case "max": aggregate = ScalarAggregate.Max; return true;
            default: aggregate = ScalarAggregate.First; return false;
        }
    }
}
