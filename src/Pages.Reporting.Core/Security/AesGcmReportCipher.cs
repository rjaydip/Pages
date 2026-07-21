using System.Security.Cryptography;
using System.Text;

namespace Pages.Reporting.Core.Security;

/// <summary>
/// AES-256-GCM cipher. Token format: <c>enc:v1:base64(nonce | tag | ciphertext)</c>.
/// The version prefix allows future algorithm migration without breaking stored reports.
/// </summary>
public sealed class AesGcmReportCipher : IReportCipher
{
    public const string Prefix = "enc:v1:";

    private const int NonceSize = 12;
    private const int TagSize = 16;

    private readonly byte[] _key;

    /// <param name="key">A 256-bit (32 byte) key.</param>
    public AesGcmReportCipher(byte[] key)
    {
        if (key.Length != 32)
            throw new ArgumentException("Encryption key must be exactly 32 bytes (AES-256).", nameof(key));
        _key = key;
    }

    public bool IsEncrypted(string value) => value.StartsWith(Prefix, StringComparison.Ordinal);

    public string Encrypt(string plaintext)
    {
        var plainBytes = Encoding.UTF8.GetBytes(plaintext);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var tag = new byte[TagSize];
        var cipherBytes = new byte[plainBytes.Length];

        using (var aes = new AesGcm(_key, TagSize))
            aes.Encrypt(nonce, plainBytes, cipherBytes, tag);

        var packed = new byte[NonceSize + TagSize + cipherBytes.Length];
        nonce.CopyTo(packed, 0);
        tag.CopyTo(packed, NonceSize);
        cipherBytes.CopyTo(packed, NonceSize + TagSize);

        return Prefix + Convert.ToBase64String(packed);
    }

    public string Decrypt(string token)
    {
        if (!IsEncrypted(token))
            throw new FormatException("Value is not an encrypted token (missing enc:v1: prefix).");

        byte[] packed;
        try
        {
            packed = Convert.FromBase64String(token[Prefix.Length..]);
        }
        catch (FormatException ex)
        {
            throw new FormatException("Encrypted token is not valid base64.", ex);
        }

        if (packed.Length < NonceSize + TagSize)
            throw new FormatException("Encrypted token is too short.");

        var nonce = packed.AsSpan(0, NonceSize);
        var tag = packed.AsSpan(NonceSize, TagSize);
        var cipherBytes = packed.AsSpan(NonceSize + TagSize);
        var plainBytes = new byte[cipherBytes.Length];

        try
        {
            using var aes = new AesGcm(_key, TagSize);
            aes.Decrypt(nonce, cipherBytes, tag, plainBytes);
        }
        catch (AuthenticationTagMismatchException ex)
        {
            throw new CryptographicException(
                "Could not decrypt the connection string. The report was encrypted with a different key " +
                "(check PAGES_REPORTING_KEY / the configured encryption key / pages-reporting.key).", ex);
        }

        return Encoding.UTF8.GetString(plainBytes);
    }
}
