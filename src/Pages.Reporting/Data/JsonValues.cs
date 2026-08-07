using System.Text.Json;

namespace Pages.Reporting.Core.Data;

/// <summary>Converts JSON values from deserialized report definitions into CLR values.</summary>
public static class JsonValues
{
    public static object? ToClr(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Null or JsonValueKind.Undefined => null,
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.String => element.GetString(),
        JsonValueKind.Number when element.TryGetInt64(out var l) => l,
        JsonValueKind.Number => element.GetDouble(),
        _ => element.GetRawText(),
    };
}
