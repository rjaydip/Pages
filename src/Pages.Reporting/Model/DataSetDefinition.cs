using Pages.Reporting.Core.Data;

namespace Pages.Reporting.Core.Model;

/// <summary>
/// A named, report-level data source: one connection + one query (or static rows),
/// executed once per generation and shared by every element that references it
/// through a <see cref="DataSetBinding"/>.
/// </summary>
public sealed class DataSetDefinition
{
    public required string Name { get; set; }

    /// <summary>How the rows are produced — a <see cref="SqlBinding"/> or <see cref="InlineBinding"/>.</summary>
    public required DataBinding Binding { get; set; }
}
