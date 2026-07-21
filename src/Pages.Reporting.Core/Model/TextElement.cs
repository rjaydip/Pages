namespace Pages.Reporting.Core.Model;

/// <summary>
/// Free text mixing literal content with data placeholders:
/// <c>{dataSet.Column}</c>, <c>{dataSet.Column:sum:C0}</c>, <c>{@paramName}</c>;
/// <c>{{</c> escapes a literal brace. Replaces the older title/value elements
/// (which remain supported for existing JSON).
/// </summary>
public sealed class TextElement : ReportElement
{
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Render the resolved content as raw HTML (author-supplied tags like &lt;b&gt; or
    /// &lt;span style="..."&gt; take effect) instead of plain text.
    /// </summary>
    public bool RenderHtml { get; set; }
}
