namespace Pages.Reporting.Core.Security;

/// <summary>Encrypts/decrypts sensitive values (connection strings) inside report JSON.</summary>
public interface IReportCipher
{
    /// <summary>Encrypts a plaintext value into a self-describing token (e.g. "enc:v1:...").</summary>
    string Encrypt(string plaintext);

    /// <summary>Decrypts a token produced by <see cref="Encrypt"/>.</summary>
    string Decrypt(string token);

    /// <summary>True if the value is already an encrypted token.</summary>
    bool IsEncrypted(string value);
}
