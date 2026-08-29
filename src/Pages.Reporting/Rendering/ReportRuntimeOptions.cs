namespace Pages.Reporting.Core.Rendering;

/// <summary>
/// Host-supplied inputs for one report generation — the values the caller provides at
/// generation time rather than baking into the report JSON. Every member is optional, and a
/// null <see cref="ReportRuntimeOptions"/> is equivalent to one with everything unset.
/// </summary>
public sealed record ReportRuntimeOptions
{
    /// <summary>
    /// Values for the report's declared parameters, keyed by parameter name (case-insensitive,
    /// a leading <c>@</c> is ignored). Covers both reader fill-in parameters and host-supplied
    /// ones (see <see cref="Model.ParameterDefinition.AcceptsUserInput"/>). Only declared
    /// parameters are applied; unknown keys are ignored. An absent key keeps the declared
    /// default; a key present with a null or empty value is treated as absent. A value reaches
    /// a data set's SQL only when that query text contains <c>@name</c>.
    /// </summary>
    public IReadOnlyDictionary<string, string?>? Parameters { get; init; }

    /// <summary>
    /// Connection strings supplied at generation time, keyed by connection name
    /// (case-insensitive). Only connections the report marks
    /// <see cref="Model.ConnectionDefinition.SuppliedAtRuntime"/> read from here; keys that do
    /// not match such a connection are ignored. The value is used as-is (plaintext) and the
    /// database provider is always taken from the report's connection definition. A connection
    /// string is never persisted or exposed on <see cref="ResolvedReport"/>.
    /// </summary>
    public IReadOnlyDictionary<string, string?>? ConnectionStrings { get; init; }
}
