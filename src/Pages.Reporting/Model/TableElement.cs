using Pages.Reporting.Core.Data;

namespace Pages.Reporting.Core.Model;

/// <summary>A tabular report section bound to a row-producing data source.</summary>
public sealed class TableElement : ReportElement
{
    public string? Title { get; set; }

    public required DataBinding Binding { get; set; }

    /// <summary>Columns to display. Empty list means: show every column the query returns.</summary>
    public List<ColumnDefinition> Columns { get; set; } = [];
}

public sealed class ColumnDefinition
{
    /// <summary>Field (column) name in the result set.</summary>
    public required string Field { get; set; }

    /// <summary>Header text; defaults to the field name.</summary>
    public string? Header { get; set; }

    /// <summary>.NET format string, e.g. "N2", "C", "d".</summary>
    public string? Format { get; set; }

    public ColumnAlignment Align { get; set; } = ColumnAlignment.Auto;

    public ColumnAggregate Aggregate { get; set; } = ColumnAggregate.None;
}

public enum ColumnAlignment
{
    /// <summary>Right for numeric values, left otherwise.</summary>
    Auto,
    Left,
    Center,
    Right,
}

public enum ColumnAggregate
{
    None,
    Sum,
    Average,
    Count,
    Min,
    Max,
}
