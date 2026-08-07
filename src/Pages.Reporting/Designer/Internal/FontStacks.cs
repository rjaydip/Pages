namespace Pages.Reporting.Designer.Internal;

/// <summary>
/// The fonts the style editor offers, grouped for the dropdown's optgroups.
///
/// Every stack ends in a generic family, so headless Chromium resolves something even on a
/// container with almost no fonts installed — the PDF may not get the first choice, but it
/// never falls back to nothing.
///
/// Labels marked "(web)" are popular faces that are NOT installed on a typical server. Without
/// an @font-face from the consuming app the PDF renders the next entry in the stack instead, so
/// the label says so at the point of choosing rather than in documentation nobody reads.
/// </summary>
internal static class FontStacks
{
    internal sealed record FontStack(string Group, string Label, string Stack);

    /// <summary>Declaration order is display order; GroupBy preserves it for the optgroups.</summary>
    public static readonly FontStack[] All =
    [
        new("Sans-serif", "System UI", "system-ui, -apple-system, 'Segoe UI', sans-serif"),
        new("Sans-serif", "Arial", "Arial, Helvetica, sans-serif"),
        // Condensed type is what rescues a table one column too wide — worth a slot.
        new("Sans-serif", "Arial Narrow", "'Arial Narrow', Arial, sans-serif"),
        new("Sans-serif", "Helvetica", "Helvetica, Arial, sans-serif"),
        new("Sans-serif", "Verdana", "Verdana, Geneva, sans-serif"),
        new("Sans-serif", "Tahoma", "Tahoma, Verdana, sans-serif"),
        new("Sans-serif", "Trebuchet MS", "'Trebuchet MS', Tahoma, sans-serif"),
        new("Sans-serif", "Segoe UI", "'Segoe UI', Roboto, sans-serif"),
        new("Sans-serif", "Calibri", "Calibri, Candara, sans-serif"),
        new("Sans-serif", "Century Gothic", "'Century Gothic', Futura, sans-serif"),
        new("Sans-serif", "Roboto (web)", "Roboto, 'Helvetica Neue', Arial, sans-serif"),
        new("Sans-serif", "Open Sans (web)", "'Open Sans', 'Segoe UI', sans-serif"),
        new("Sans-serif", "Lato (web)", "Lato, 'Trebuchet MS', sans-serif"),
        new("Sans-serif", "Montserrat (web)", "Montserrat, 'Segoe UI', sans-serif"),
        new("Sans-serif", "Inter (web)", "Inter, 'Segoe UI', sans-serif"),

        new("Serif", "Times New Roman", "'Times New Roman', Times, serif"),
        new("Serif", "Georgia", "Georgia, 'Times New Roman', serif"),
        new("Serif", "Cambria", "Cambria, Georgia, serif"),
        new("Serif", "Garamond", "Garamond, 'Times New Roman', serif"),
        new("Serif", "Book Antiqua", "'Book Antiqua', Palatino, 'Times New Roman', serif"),
        new("Serif", "Baskerville", "Baskerville, 'Times New Roman', serif"),
        new("Serif", "Merriweather (web)", "Merriweather, Georgia, serif"),
        new("Serif", "PT Serif (web)", "'PT Serif', Georgia, serif"),
        new("Serif", "Playfair Display (web)", "'Playfair Display', Georgia, serif"),

        new("Monospace", "Courier New", "'Courier New', Courier, monospace"),
        new("Monospace", "Consolas", "Consolas, 'Courier New', monospace"),
        new("Monospace", "Menlo", "Menlo, Consolas, 'Courier New', monospace"),
        new("Monospace", "Roboto Mono (web)", "'Roboto Mono', Consolas, monospace"),

        new("Display", "Impact", "Impact, Haettenschweiler, sans-serif"),
        new("Display", "Comic Sans MS", "'Comic Sans MS', 'Comic Sans', cursive"),
    ];

    /// <summary>
    /// True when the stored value is one of the presets. False for a hand-authored stack,
    /// which the editor then opens in its Custom box rather than silently resetting.
    /// </summary>
    public static bool IsPreset(string? fontFamily) =>
        !string.IsNullOrWhiteSpace(fontFamily) && All.Any(font => font.Stack == fontFamily);
}
