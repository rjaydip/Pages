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
                case "connectionstring":
                    connectionString = reader.GetString();
                    break;
                default:
                    reader.Skip();
                    break;
            }
        }

        if (name is null || connectionString is null)
            throw new JsonException("Connection definition requires 'name' and 'connectionString'.");

        return new ConnectionDefinition { Name = name, Provider = provider, ConnectionString = connectionString };
    }

    public override void Write(Utf8JsonWriter writer, ConnectionDefinition value, JsonSerializerOptions options)
    {
        var token = cipher.IsEncrypted(value.ConnectionString)
            ? value.ConnectionString
            : cipher.Encrypt(value.ConnectionString);

        writer.WriteStartObject();
        writer.WriteString("name", value.Name);
        writer.WriteString("provider", value.Provider.ToString());
        writer.WriteString("connectionString", token);
        writer.WriteEndObject();
    }
}
