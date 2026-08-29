namespace Pages.Reporting.Core.Model;

/// <summary>
/// One choice for a <see cref="ParameterType.List"/> parameter. <see cref="Value"/> is what a
/// query and <c>{@name}</c> receive; <see cref="Label"/> is the reader-facing text in
/// <c>&lt;ReportParameters&gt;</c> (null → show the value itself).
/// </summary>
/// <param name="Value">The value bound to SQL and shown by <c>{@name}</c>.</param>
/// <param name="Label">The text shown to the reader, or null to show <paramref name="Value"/>.</param>
public sealed record ParameterOption(string Value, string? Label = null);
