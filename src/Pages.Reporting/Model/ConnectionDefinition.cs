namespace Pages.Reporting.Core.Model;

/// <summary>
/// A named database connection embedded in the report definition.
/// The connection string is encrypted (enc:v1:...) whenever the report is serialized,
/// so the report JSON is safe to store anywhere.
/// </summary>
public sealed class ConnectionDefinition
{
    public required string Name { get; set; }

    public DatabaseProvider Provider { get; set; }

    /// <summary>
    /// When true, the connection string is not stored in the report: <see cref="ConnectionString"/>
    /// is left empty and the caller must pass one at generation time via
    /// <see cref="Rendering.ReportRuntimeOptions.ConnectionStrings"/>, keyed by <see cref="Name"/>.
    /// Lets one report run against dev/staging/prod, or a per-tenant database, without
    /// duplicating the definition. When false (the default) the stored string is always used
    /// and any runtime value for this name is ignored.
    /// </summary>
    public bool SuppliedAtRuntime { get; set; }

    /// <summary>
    /// Plaintext while building in memory; ciphertext after serialization. Empty when
    /// <see cref="SuppliedAtRuntime"/> is true.
    /// </summary>
    public required string ConnectionString { get; set; }
}
