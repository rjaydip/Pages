using Pages.Reporting.Core.Data;

namespace Pages.Reporting.Core.Rendering;

/// <summary>
/// Reduces a data set column to the single value a text token shows. Shared because grouping
/// applies exactly the same reduction to a subset of the rows — two copies would be two places
/// for "sum" to mean something slightly different.
/// </summary>
public static class ScalarValues
{
    public static object? Extract(ReportDataTable table, string? field, ScalarAggregate aggregate) =>
        Extract(table.Rows, field, aggregate);

    /// <summary>The same reduction over an arbitrary row set — a group's slice of a data set.</summary>
    public static object? Extract(
        IReadOnlyList<IReadOnlyDictionary<string, object?>> rows,
        string? field,
        ScalarAggregate aggregate)
    {
        if (string.IsNullOrWhiteSpace(field))
            return rows.Count > 0 ? rows[0].Values.FirstOrDefault() : null;

        if (aggregate == ScalarAggregate.First)
            return rows.Count > 0 ? rows[0].GetValueOrDefault(field) : null;

        var values = rows
            .Select(r => r.GetValueOrDefault(field))
            .Where(v => v is not null)
            .ToList();

        if (aggregate == ScalarAggregate.Count)
            return values.Count;

        var numbers = values.Where(ValueFormatter.IsNumeric).Select(ValueFormatter.ToDouble).ToList();
        if (numbers.Count == 0)
            return null;

        return aggregate switch
        {
            ScalarAggregate.Sum => numbers.Sum(),
            ScalarAggregate.Average => numbers.Average(),
            ScalarAggregate.Min => numbers.Min(),
            ScalarAggregate.Max => numbers.Max(),
            _ => numbers[0],
        };
    }
}
