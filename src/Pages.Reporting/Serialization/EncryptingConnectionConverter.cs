using System.Text.Json;
using System.Text.Json.Serialization;
using Pages.Reporting.Core.Model;
using Pages.Reporting.Core.Security;

namespace Pages.Reporting.Core.Serialization;

/// <summary>
/// Serializes <see cref="ConnectionDefinition"/> so the connection string is always written
/// as an encrypted token — plaintext never reaches the JSON. Already-encrypted values pass
/// through unchanged, so serialize/deserialize round trips are stable.
/// </summary>
internal sealed class EncryptingConnectionConverter(IReportCipher cipher) : JsonConverter<ConnectionDefinition>
{
    public override ConnectionDefinition Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        string? name = null, connectionString = null;
        var provider = DatabaseProvider.SqlServer;
        var suppliedAtRuntime = false;

        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException("Expected an object for a connection definition.");

        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            var property = reader.GetString();
            reader.Read();
            switch (property?.ToLowerInvariant())
            {
                case "name":
                    name = reader.GetString();
                    break;
                case "provider":
                    provider = Enum.Parse<DatabaseProvider>(reader.GetString()!, ignoreCase: true);
                    break;
                case "suppliedatruntime":
                    suppliedAtRuntime = reader.GetBoolean();
                    break;
                case "connectionstring":
                    connectionString = reader.GetString();
                    break;
                default:
                    reader.Skip();
                    break;
            }
        }

        if (name is null)
            throw new JsonException("Connection definition requires 'name'.");

        // A runtime-supplied connection carries no stored string, so the property may be
        // absent or empty; every other connection must have one.
        if (connectionString is null && !suppliedAtRuntime)
            throw new JsonException("Connection definition requires 'connectionString'.");

        return new ConnectionDefinition
        {
            Name = name,
            Provider = provider,
            SuppliedAtRuntime = suppliedAtRuntime,
            ConnectionString = connectionString ?? string.Empty,
        };
    }

    public override void Write(Utf8JsonWriter writer, ConnectionDefinition value, JsonSerializerOptions options)
    {
        // A runtime-supplied connection stores no string. A blank string on any other
        // connection is written through as "" rather than an encrypted empty token: the
        // cipher uses a random nonce, so Encrypt("") would churn the JSON on every save and
        // still round-trip back through this branch (IsEncrypted("") is false).
        var token = value.SuppliedAtRuntime || string.IsNullOrWhiteSpace(value.ConnectionString)
            ? string.Empty
            : cipher.IsEncrypted(value.ConnectionString)
                ? value.ConnectionString
                : cipher.Encrypt(value.ConnectionString);

        writer.WriteStartObject();
        writer.WriteString("name", value.Name);
        writer.WriteString("provider", value.Provider.ToString());
        if (value.SuppliedAtRuntime)
            writer.WriteBoolean("suppliedAtRuntime", true);
        writer.WriteString("connectionString", token);
        writer.WriteEndObject();
    }
}
