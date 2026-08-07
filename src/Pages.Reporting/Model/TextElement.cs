namespace Pages.Reporting.Core.Model;

/// <summary>
/// Free text mixing literal content with data placeholders:
/// <c>{dataSet.Column}</c>, <c>{dataSet.Column:sum:C0}</c>, <c>{@paramName}</c>;
/// <c>{{</c> escapes a literal brace. The report's only single-value component — a label,
/// a figure and a caption are all just text with tokens in it.
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
