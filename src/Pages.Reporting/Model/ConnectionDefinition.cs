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

    /// <summary>Plaintext while building in memory; ciphertext after serialization.</summary>
    public required string ConnectionString { get; set; }
}
