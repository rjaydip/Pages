using System.Text;
using Pages.Reporting.Core.Model;

namespace Pages.Reporting.Blazor;

/// <summary>Turns <see cref="ElementStyle"/> into inline CSS declarations.</summary>
public static class StyleCss
{
    /// <summary>All declarations — for components with a single root element.</summary>
    public static string? Inline(ElementStyle? style) =>
        style is null ? null : Concat(Text(style), Container(style));

    /// <summary>Text-level declarations (weight, italic, size) — e.g. for a title's h1.</summary>
    public static string? Text(ElementStyle? style)
    {
        if (style is null)
            return null;
        var css = new StringBuilder();
        if (style.Bold) css.Append("font-weight:700;");
        if (style.Italic) css.Append("font-style:italic;");
        if (style.FontSize is { } size) css.Append($"font-size:{size}px;");
        return NullIfEmpty(css);
    }

    /// <summary>
    /// Block-level declarations (align, colors, padding, border) — for the container.
    /// Standard CSS box: border at the box edge, padding inside it — so with spacing 0,
    /// neighboring boxes' borders truly meet (per-side borders give single shared lines).
    /// </summary>
    public static string? Container(ElementStyle? style)
    {
        if (style is null)
            return null;
        var css = new StringBuilder();
        if (style.Align != TextAlign.Default) css.Append($"text-align:{style.Align.ToString().ToLowerInvariant()};");
        if (style.VerticalAlign != VerticalAlign.Default)
        {
            // The element fills its grid cell (pr-cell stretches it), so flex placement
            // positions the content within that height — identically in the PDF.
            css.Append("display:flex;flex-direction:column;justify-content:");
            css.Append(style.VerticalAlign switch
            {
                VerticalAlign.Middle => "center;",
                VerticalAlign.Bottom => "flex-end;",
                _ => "flex-start;",
            });
        }
        if (!string.IsNullOrWhiteSpace(style.Color)) css.Append($"color:{style.Color};");
        if (!string.IsNullOrWhiteSpace(style.Background)) css.Append($"background:{style.Background};");
        if (!string.IsNullOrWhiteSpace(style.Padding)) css.Append($"padding:{WithUnits(style.Padding!)};");
        if (style.HasBorder) css.Append(BorderCss(style));
        return NullIfEmpty(css);
    }

    /// <summary>Custom CSS shorthand wins; otherwise per-side lines from the structured fields.</summary>
    private static string BorderCss(ElementStyle style)
    {
        if (!string.IsNullOrWhiteSpace(style.Border))
            return $"border:{style.Border};";

        var line = $"{Math.Max(1, style.BorderWidth ?? 1)}px solid " +
                   (string.IsNullOrWhiteSpace(style.BorderColor) ? "#808080" : style.BorderColor);
        if (style is { BorderTop: true, BorderRight: true, BorderBottom: true, BorderLeft: true })
            return $"border:{line};";

        var css = new StringBuilder();
        if (style.BorderTop) css.Append($"border-top:{line};");
        if (style.BorderRight) css.Append($"border-right:{line};");
        if (style.BorderBottom) css.Append($"border-bottom:{line};");
        if (style.BorderLeft) css.Append($"border-left:{line};");
        return css.ToString();
    }

    /// <summary>
    /// Page style, on screen. Standard box order: print margin (the sheet's padding) →
    /// border → page padding → components. The box stretches to fill the sheet
    /// (PageSheet renders a flex column), so the border frames the whole page.
    /// </summary>
    public static string? Page(ElementStyle? style)
    {
        if (style is null)
            return null;
        var css = new StringBuilder();
        AppendInherited(css, style);
        if (!string.IsNullOrWhiteSpace(style.Background)) css.Append($"background:{style.Background};");
        if (style.HasBorder) css.Append(BorderCss(style));
        if (!string.IsNullOrWhiteSpace(style.Padding)) css.Append($"padding:{WithUnits(style.Padding!)};");
        // align-content keeps the grid's auto rows at their natural height while the
        // stretched box's leftover space stays empty below the content.
        if (style.HasBorder || !string.IsNullOrWhiteSpace(style.Background)) css.Append("flex:1 1 auto;align-content:start;");
        return NullIfEmpty(css);
    }

    /// <summary>
    /// Page style, print, for the report root: inherited text styles plus the page
    /// padding, cloned onto every page fragment (box-decoration-break) so the gap
    /// inside the border repeats at page breaks. Border and background live on the
    /// fixed per-page frame — so the order stays margin → border → padding → content.
    /// </summary>
    public static string? PageContent(ElementStyle? style)
    {
        if (style is null)
            return null;
        var css = new StringBuilder();
        AppendInherited(css, style);
        if (PrintPadding(style) is { } padding)
            css.Append($"padding:{padding};box-decoration-break:clone;-webkit-box-decoration-break:clone;");
        return NullIfEmpty(css);
    }

    /// <summary>With no explicit padding, a border still needs content nudged off its line.</summary>
    private static string? PrintPadding(ElementStyle style) =>
        !string.IsNullOrWhiteSpace(style.Padding) ? WithUnits(style.Padding!)
        : style.HasBorder ? $"{Math.Max(1, style.BorderWidth ?? 1)}px"
        : null;

    /// <summary>
    /// Page style, print, for the fixed per-page frame: border + background, repeated
    /// on every printed page (Chromium repeats position:fixed elements). At inset:0 it
    /// spans the full page just inside the print margins — a full-height frame even
    /// where content ends early — while the root's cloned padding keeps content off it.
    /// </summary>
    public static string? PageFrame(ElementStyle? style)
    {
        if (style is null)
            return null;
        var css = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(style.Background)) css.Append($"background:{style.Background};");
        if (style.HasBorder) css.Append(BorderCss(style));
        return NullIfEmpty(css);
    }

    private static void AppendInherited(StringBuilder css, ElementStyle style)
    {
        if (Text(style) is { } text) css.Append(text);
        if (style.Align != TextAlign.Default) css.Append($"text-align:{style.Align.ToString().ToLowerInvariant()};");
        if (!string.IsNullOrWhiteSpace(style.Color)) css.Append($"color:{style.Color};");
    }

    /// <summary>Normalizes a single CSS length (bare number → px); null/blank → null.</summary>
    public static string? Length(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : WithUnits(value.Trim());

    /// <summary>The layout grid's gap ("row column"); null/blank → the 12px/16px default.</summary>
    public static string Gap(string? spacing) =>
        string.IsNullOrWhiteSpace(spacing) ? "12px 16px" : WithUnits(spacing.Trim());

    /// <summary>"20" or "8 16" are invalid CSS lengths — treat bare numbers as pixels.</summary>
    private static string WithUnits(string value) =>
        string.Join(' ', value.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Length > 0 && part.All(char.IsDigit) ? part + "px" : part));

    public static string? Concat(string? first, string? second) =>
        (first, second) switch
        {
            (null, null) => null,
            (null, _) => second,
            (_, null) => first,
            _ => first + second,
        };

    private static string? NullIfEmpty(StringBuilder css) => css.Length == 0 ? null : css.ToString();
}
