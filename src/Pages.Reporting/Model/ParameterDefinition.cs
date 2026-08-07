namespace Pages.Reporting.Core.Model;

/// <summary>
/// A named report parameter. Data set SQL references it as <c>@name</c>; the host
/// supplies a runtime value at generation time (e.g. taken from the page URL),
/// falling back to <see cref="DefaultValue"/> when none is given.
/// </summary>
public sealed class ParameterDefinition
{
    public required string Name { get; set; }

    public string? DefaultValue { get; set; }
}
