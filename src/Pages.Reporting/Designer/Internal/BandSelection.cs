using Pages.Reporting.Core.Model;

namespace Pages.Reporting.Designer.Internal;

/// <summary>Selection marker meaning "this header/footer band itself" (vs an element inside it).</summary>
internal sealed class BandSelection(PageBand band)
{
    public PageBand Band { get; } = band;
}
