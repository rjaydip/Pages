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
/// Rows consumers (table/chart) use only <see cref="Name"/> and get the data set's rows.
/// <see cref="Field"/> and <see cref="Aggregate"/> are inert here: <c>TextElement</c> has no
/// binding of its own, so they never come from a stored <see cref="DataSetBinding"/>. Instead
/// <c>ReportDataResolver</c> parses a <c>{dataSet.Column:aggregate:format}</c> token and builds
/// a throwaway <see cref="DataSetBinding"/> from its pieces, just to reuse the same scalar
/// extraction as everything else — these two properties are that argument bundle, not a shape
/// a text token reads from.
/// </summary>
public sealed class DataSetBinding : DataBinding
{
    /// <summary>Name of a <c>DataSetDefinition</c> in the report's data sets list.</summary>
    public required string Name { get; set; }

    /// <summary>
    /// Column a text token reduces to one value. Only ever set on the throwaway binding
    /// <c>ReportDataResolver</c> builds from a parsed token; ignored on a stored binding and by
    /// rows consumers.
    /// </summary>
    public string? Field { get; set; }

    /// <summary>
    /// How a text token reduces the column to one value (default: first row's value). Same
    /// caveat as <see cref="Field"/> — meaningful only on the resolver's throwaway binding.
    /// </summary>
    public ScalarAggregate Aggregate { get; set; }
}

/// <summary>Reduction a text token applies to a data set column to reach its one value.</summary>
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
