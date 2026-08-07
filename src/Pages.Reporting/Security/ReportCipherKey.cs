using System.Security.Cryptography;
using System.Text;

namespace Pages.Reporting.Core.Security;

/// <summary>
/// Resolves the AES-256 key used to protect connection strings, in priority order:
/// 1. an explicitly configured key, 2. the PAGES_REPORTING_KEY environment variable,
/// 3. an auto-generated key file next to the application (works out of the box on one machine).
/// </summary>
public static class ReportCipherKey
{
    public const string EnvironmentVariable = "PAGES_REPORTING_KEY";
    public const string KeyFileName = "pages-reporting.key";

    public static byte[] Resolve(string? configuredKey, string? keyFilePath = null)
    {
        if (!string.IsNullOrWhiteSpace(configuredKey))
            return FromString(configuredKey);

        var env = Environment.GetEnvironmentVariable(EnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(env))
            return FromString(env);

        return FromKeyFile(keyFilePath ?? Path.Combine(AppContext.BaseDirectory, KeyFileName));
    }

    /// <summary>Accepts base64 of exactly 32 bytes, otherwise treats the string as a passphrase (SHA-256 derived).</summary>
    private static byte[] FromString(string value)
    {
        try
        {
            var bytes = Convert.FromBase64String(value);
            if (bytes.Length == 32)
                return bytes;
        }
        catch (FormatException)
        {
            // not base64 — fall through to passphrase derivation
        }

        return SHA256.HashData(Encoding.UTF8.GetBytes(value));
    }

    private static byte[] FromKeyFile(string path)
    {
        if (File.Exists(path))
            return FromString(File.ReadAllText(path).Trim());

        var key = RandomNumberGenerator.GetBytes(32);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, Convert.ToBase64String(key));
        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        return key;
    }
}
