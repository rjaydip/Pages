namespace Pages.Reporting.Core.Model;

/// <summary>
/// What a <see cref="BandType.GroupHeader"/> partitions: a data set by one of its columns.
/// The matching <see cref="BandType.GroupFooter"/> carries no binding of its own — it finds
/// this one by matching <see cref="PageBand.Level"/>.
/// </summary>
public sealed class GroupBinding
{
    /// <summary>Name of a <c>DataSetDefinition</c> in the report's data sets list.</summary>
    public required string DataSet { get; set; }

    /// <summary>Column to group by. Rows are partitioned by first appearance, never sorted.</summary>
    public required string Field { get; set; }
}
