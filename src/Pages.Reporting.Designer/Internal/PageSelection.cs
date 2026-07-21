namespace Pages.Reporting.Designer.Internal;

/// <summary>Selection marker meaning "the page itself" (vs an element or a data-tree item).</summary>
internal sealed class PageSelection
{
    public static readonly PageSelection Instance = new();

    private PageSelection()
    {
    }
}
