using System.Text.Json.Serialization;

namespace Pages.Reporting.Core.Model;

/// <summary>
/// Optional built-in styling for an element (or, on <see cref="Report.Style"/>, the page).
/// Rendered as inline styles so the PDF matches the screen without any stylesheet.
/// Everything here is optional — for full control use your own CSS via CssClass instead.
/// </summary>
public sealed class ElementStyle
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool Bold { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool Italic { get; set; }

    /// <summary>Font size in pixels.</summary>
    public int? FontSize { get; set; }

    /// <summary>
    /// CSS font stack, e.g. "Georgia, 'Times New Roman', serif". Null inherits.
    /// A stack rather than one name: the PDF renders wherever Chromium runs, which may
    /// not have the first choice installed, so the fallbacks are what keep it readable.
    /// </summary>
    public string? FontFamily { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public TextAlign Align { get; set; }

    /// <summary>Vertical position of the content within the element's box/cell.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public VerticalAlign VerticalAlign { get; set; }

    /// <summary>Text color — any CSS color, e.g. "#333333" or "crimson".</summary>
    public string? Color { get; set; }

    /// <summary>Background — any CSS color.</summary>
    public string? Background { get; set; }

    /// <summary>CSS padding shorthand, e.g. "8px" or "8px 16px".</summary>
    public string? Padding { get; set; }

    /// <summary>CSS border shorthand, e.g. "1px solid #808080". Overrides the structured border fields.</summary>
    public string? Border { get; set; }

    /// <summary>Structured border: line width in pixels (default 1 when a border is on).</summary>
    public int? BorderWidth { get; set; }

    /// <summary>Structured border: line color (default #808080).</summary>
    public string? BorderColor { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool BorderTop { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool BorderRight { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool BorderBottom { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool BorderLeft { get; set; }

    /// <summary>True when any border is configured (custom CSS or structured sides).</summary>
    [JsonIgnore]
    public bool HasBorder =>
        !string.IsNullOrWhiteSpace(Border) || BorderTop || BorderRight || BorderBottom || BorderLeft;
}

public enum TextAlign
{
    Default,
    Left,
    Center,
    Right,
}

public enum VerticalAlign
{
    Default,
    Top,
    Middle,
    Bottom,
}
