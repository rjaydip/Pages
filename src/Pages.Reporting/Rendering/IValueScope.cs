using Pages.Reporting.Core.Data;

namespace Pages.Reporting.Core.Rendering;

/// <summary>Outcome of a <c>{dataSet.Column}</c> lookup.</summary>
internal enum ColumnStatus
{
    /// <summary>Resolved — <see cref="ColumnResult.Value"/> holds the (possibly null) value.</summary>
    Ok,

    /// <summary>The report declares no data set by that name.</summary>
    UnknownDataSet,

    /// <summary>The data set's query failed — <see cref="ColumnResult.Error"/> holds the message.</summary>
    DataSetError,
}

/// <summary>The result of resolving a <c>{dataSet.Column:agg}</c> reference against a scope.</summary>
internal readonly record struct ColumnResult(ColumnStatus Status, object? Value, string? Error)
{
    public static ColumnResult Found(object? value) => new(ColumnStatus.Ok, value, null);

    public static ColumnResult UnknownDataSet { get; } = new(ColumnStatus.UnknownDataSet, null, null);

    public static ColumnResult Failed(string? error) => new(ColumnStatus.DataSetError, null, error);
}

/// <summary>Outcome of a <c>{group.Column}</c> lookup.</summary>
internal enum GroupStatus
{
    /// <summary>Not rendering inside a group — the token contributes nothing.</summary>
    NotInGroup,

    /// <summary>Resolved — the out value holds the group's value of the column.</summary>
    Ok,

    /// <summary>No enclosing group partitions a column by that name.</summary>
    NoSuchField,
}

/// <summary>
/// The values a <see cref="TextRenderer"/> can resolve for one render pass. Two
/// implementations back it: one over the resolver's caches (whole data set, no group), one
/// over a resolved report and a group scope (the group's row subset). Having both go through
/// this interface is what keeps resolve-time substitution and per-group re-render from
/// drifting apart.
/// </summary>
internal interface IValueScope
{
    /// <summary>A declared report parameter's effective value. A found value may be null.</summary>
    bool TryParameter(string name, out string? value);

    /// <summary>A value reduced from a data set column.</summary>
    ColumnResult Column(string dataSet, string? column, ScalarAggregate aggregate);

    /// <summary>The enclosing group's value of a column (blank column = this group's own value).</summary>
    GroupStatus Group(string? column, out object? value);

    /// <summary>The report's name — for <c>{reportName}</c>.</summary>
    string ReportName { get; }

    /// <summary>Generation time, captured once per resolution — for <c>{now}</c>.</summary>
    DateTime Now { get; }
}
