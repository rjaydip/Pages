namespace Pages.Reporting.Core.Rendering;

/// <summary>
/// Host-supplied inputs for one report generation — the values the caller provides at
/// generation time rather than baking into the report JSON. Every member is optional, and a
/// null <see cref="ReportRuntimeOptions"/> is equivalent to one with everything unset.
/// </summary>
public sealed record ReportRuntimeOptions
{
    /// <summary>
    /// Values overriding the report's declared parameter defaults, keyed by parameter name
    /// (case-insensitive, a leading <c>@</c> is ignored). Only declared parameters are
    /// applied; unknown keys are ignored. An absent key keeps the declared default; a key
    /// present with a null or empty value is treated as absent.
    /// </summary>
    public IReadOnlyDictionary<string, string?>? Parameters { get; init; }

    /// <summary>
    /// Connection strings supplied at generation time, keyed by connection name
    /// (case-insensitive). Only connections the report marks
    /// <see cref="Model.ConnectionDefinition.SuppliedAtRuntime"/> read from here; keys that do
    /// not match such a connection are ignored. The value is used as-is (plaintext) and the
    /// database provider is always taken from the report's connection definition. Nothing here
    /// is persisted or exposed on <see cref="ResolvedReport"/>.
    /// </summary>
    public IReadOnlyDictionary<string, string?>? ConnectionStrings { get; init; }
}
