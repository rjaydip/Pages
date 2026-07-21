using System.Text.Json;
using System.Text.Json.Serialization;

namespace Pages.Reporting.Core.Data;

/// <summary>
/// Declarative description of where an element's data comes from.
/// Serializable — no delegates, no live connections.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(SqlBinding), "sql")]
[JsonDerivedType(typeof(InlineBinding), "inline")]
[JsonDerivedType(typeof(DataSetBinding), "dataSet")]
public abstract class DataBinding;

/// <summary>
/// Reads from a shared, report-level data set (declared in <c>Report.DataSets</c>).
/// Rows consumers (table/chart) get the data set's rows; scalar consumers (value/KPI)
/// extract one value from <see cref="Field"/> via <see cref="Aggregate"/>.
/// </summary>
public sealed class DataSetBinding : DataBinding
{
    /// <summary>Name of a <c>DataSetDefinition</c> in the report's data sets list.</summary>
    public required string Name { get; set; }

    /// <summary>Column to read for scalar consumers ("data.column"); ignored for rows consumers.</summary>
    public string? Field { get; set; }

    /// <summary>How scalar consumers reduce the column to one value. Default: first row's value.</summary>
    public ScalarAggregate Aggregate { get; set; }
}

/// <summary>Reduction applied by scalar consumers of a data set column.</summary>
public enum ScalarAggregate
{
    First,
    Sum,
    Average,
    Count,
    Min,
    Max,
}

/// <summary>A SQL query executed against a named connection declared in the report.</summary>
public sealed class SqlBinding : DataBinding
{
    /// <summary>Name of a <c>ConnectionDefinition</c> in the report's connections list.</summary>
    public required string Connection { get; set; }

    public required string Sql { get; set; }

    /// <summary>Parameter values referenced in the SQL as @name.</summary>
    public Dictionary<string, JsonElement>? Parameters { get; set; }
}

/// <summary>Literal rows embedded in the report JSON — for static data that needs no database.</summary>
public sealed class InlineBinding : DataBinding
{
    public List<Dictionary<string, JsonElement>> Rows { get; set; } = [];
}
