using Pages.Reporting.Core.Data;
using Pages.Reporting.Core.Model;

namespace Pages.Reporting.Designer.Internal;

/// <summary>
/// Checks the parameter list for authoring mistakes the reader would otherwise only discover
/// when a query returns nothing: a default that doesn't match its type, a list with no
/// choices, a blank or duplicate name. Warnings, never errors — the report still renders.
/// </summary>
internal static class ParameterChecks
{
    public static IReadOnlyList<string> Problems(Report report)
    {
        var problems = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var parameter in report.Parameters)
        {
            var name = parameter.Name?.Trim() ?? string.Empty;

            if (name.Length == 0)
            {
                problems.Add("A parameter has no name — it cannot be referenced and will be ignored.");
                continue;
            }
            var key = name.TrimStart('@');
            if (!seen.Add(key))
                problems.Add($"Parameter '{key}' is declared more than once — only the last one is used.");

            if (parameter.Type is ParameterType.List)
            {
                if (parameter.AllowedValues is not { Count: > 0 })
                {
                    problems.Add($"Parameter '{key}' is a list but has no allowed values.");
                    continue;
                }

                var values = new HashSet<string>(StringComparer.Ordinal);
                foreach (var option in parameter.AllowedValues)
                    if (!values.Add(option.Value))
                        problems.Add($"Parameter '{key}' has a duplicate allowed value '{option.Value}'.");
            }

            if (string.IsNullOrEmpty(parameter.DefaultValue) || parameter.Type is ParameterType.Text)
                continue;

            var parsed = ParameterParsing.TryParse(parameter.DefaultValue, parameter.Type, parameter.AllowedValues);
            if (!parsed.Valid)
                problems.Add(DescribeBadDefault(key, parameter.DefaultValue, parameter.Type));
        }

        return problems;
    }

    private static string DescribeBadDefault(string name, string value, ParameterType type) => type switch
    {
        ParameterType.Number => $"Parameter '{name}': default '{value}' is not a valid number.",
        ParameterType.Date => $"Parameter '{name}': default '{value}' is not a valid date (expected YYYY-MM-DD).",
        ParameterType.Boolean => $"Parameter '{name}': default '{value}' must be true / false or 1 / 0.",
        ParameterType.List => $"Parameter '{name}': default '{value}' is not one of the allowed values.",
        _ => $"Parameter '{name}': default '{value}' is not valid.",
    };
}
