using System.Text.Json.Serialization;

namespace Pages.Reporting.Core.Model;

/// <summary>
/// A named report parameter. Data set SQL references it as <c>@name</c> and text as
/// <c>{@name}</c>; the host supplies a runtime value at generation time (e.g. taken from the
/// page URL), falling back to <see cref="DefaultValue"/> when none is given.
/// </summary>
public sealed class ParameterDefinition
{
    public required string Name { get; set; }

    public string? DefaultValue { get; set; }

    /// <summary>
    /// True (the default) — the parameter is a fill-in field the report reader can set, and it
    /// appears in <c>&lt;ReportParameters&gt;</c>. False — it is supplied by the host
    /// application only (the signed-in user, the tenant, …) and never shown to the reader. The
    /// distinction is UI only: either way the host passes the value at generation time and it
    /// is usable as <c>{@name}</c> in text and <c>@name</c> in SQL.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool AcceptsUserInput { get; set; } = true;
}
