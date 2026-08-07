namespace Pages.Reporting.Core.Rendering;

/// <summary>Built-in values usable in text content as bare tokens, e.g. <c>{page}</c>.</summary>
public enum BuiltinToken
{
    None,

    /// <summary>Current page number — only a real number inside a repeating band's PDF output.</summary>
    Page,

    /// <summary>Total page count — see <see cref="Page"/>.</summary>
    Pages,

    /// <summary>The report's name.</summary>
    ReportName,

    /// <summary>Generation time; honours a .NET format, e.g. <c>{now:yyyy-MM-dd}</c>.</summary>
    Now,
}

/// <summary>
/// How <see cref="PageTokens"/> placeholders are turned into output.
/// </summary>
public enum PageTokenMode
{
    /// <summary>Screen/preview: no pagination exists, so page tokens read as page 1.</summary>
    Screen,

    /// <summary>Chromium's header/footer template, where it substitutes the real numbers.</summary>
    PdfTemplate,

    /// <summary>Plain text output (Excel) — numbers, never markup.</summary>
    Plain,
}

/// <summary>
/// Page numbers aren't knowable when data is resolved — only Chromium knows them, and only
/// while printing. The resolver therefore emits private-use placeholder characters, which
/// survive HTML escaping untouched, and each renderer swaps them for what it can show.
/// </summary>
public static class PageTokens
{
    /// <summary>Placeholder standing in for the current page number.</summary>
    public const char PagePlaceholder = '';

    /// <summary>Placeholder standing in for the total page count.</summary>
    public const char PagesPlaceholder = '';

    public static bool Contains(string? text) =>
        !string.IsNullOrEmpty(text) &&
        (text.Contains(PagePlaceholder) || text.Contains(PagesPlaceholder));

    /// <summary>
    /// Replaces the placeholders for the given target. In <see cref="PageTokenMode.PdfTemplate"/>
    /// the result contains markup, so the caller must emit it unescaped; the other modes
    /// return plain text.
    /// </summary>
    public static string Resolve(string? text, PageTokenMode mode)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        var (page, pages) = mode switch
        {
            // Chromium fills these spans on every printed page.
            PageTokenMode.PdfTemplate => ("<span class=\"pageNumber\"></span>", "<span class=\"totalPages\"></span>"),
            _ => ("1", "1"),
        };

        return text.Replace(PagePlaceholder.ToString(), page)
                   .Replace(PagesPlaceholder.ToString(), pages);
    }
}
