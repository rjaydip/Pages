using System.Text.Json;
using System.Text.Json.Serialization;
using Pages.Reporting.Core.Model;
using Pages.Reporting.Core.Security;

namespace Pages.Reporting.Core.Serialization;

/// <summary>
/// The report JSON contract: <see cref="Serialize"/> produces a self-contained JSON string
/// (connection strings encrypted) that the caller stores wherever they want;
/// <see cref="Deserialize"/> turns it back into a <see cref="Report"/> for generation.
/// </summary>
public sealed class ReportJson
{
    private readonly JsonSerializerOptions _options;

    public ReportJson(IReportCipher cipher)
    {
        _options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters =
            {
                new JsonStringEnumConverter(JsonNamingPolicy.CamelCase),
                new EncryptingConnectionConverter(cipher),
            },
        };
    }

    public string Serialize(Report report) => JsonSerializer.Serialize(report, _options);

    public Report Deserialize(string json) =>
        JsonSerializer.Deserialize<Report>(json, _options)
        ?? throw new JsonException("The JSON did not contain a report definition.");
}
