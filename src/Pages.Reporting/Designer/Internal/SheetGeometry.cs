using Pages.Reporting.Core.Model;
using Pages.Reporting.Blazor;

namespace Pages.Reporting.Designer.Internal;

/// <summary>
/// Where a band sits on the sheet, in millimetres from the page's top-left. <paramref name="Y"/>
/// is the canvas position — it carries the designer-only separation (see
/// <see cref="SheetGeometry.DesignGapMm"/>) and is what everything drawn and hit-tested uses.
/// <paramref name="PrintY"/> is the same band's offset in the printed report, and exists solely
/// so the page-break guide can be computed in print space and mapped back.
/// </summary>
internal sealed record BandBox(PageBand Band, int Index, double Y, double Height, double PrintY);

/// <summary>
/// Lays the band stack out down the page. CSS did this implicitly through flex; SVG has no
/// flow, so the same stacking — band box, gap — is computed explicitly here and used by both
/// the renderer and the hit-testing. The label is drawn separately, as an overlay on top of
/// each band box; it does not participate in this stack (see <see cref="LabelHeightMm"/>).
///
/// The canvas stack is NOT the printed stack: it inserts <see cref="DesignGapMm"/> between
/// bands so they read as separate objects. That is the one deliberate divergence, and every
/// band box carries the printed offset alongside the canvas one so anything that must speak in
/// print terms — the page-break guide — can convert through <see cref="CanvasY"/>.
/// </summary>
internal static class SheetGeometry
{
    /// <summary>
    /// Height of the label tab drawn over each band's top-left corner. It is an overlay, not a
    /// row: it deliberately does NOT enter the stack advance, because every millimetre the
    /// canvas spends on designer chrome is a millimetre the canvas and the printed report
    /// disagree by — and that divergence compounds down a tall report.
    /// </summary>
    public static double LabelHeightMm => BandTypes.GridMajorMm;

    /// <summary>
    /// Separation drawn between consecutive bands on the canvas ONLY — it is not in the report,
    /// not in <see cref="PageSheet.BandGapMm"/>, and never reaches the HTML/PDF/Excel output.
    /// With the report's own gap at 0 the bands abut, and two dashed borders meeting on a shared
    /// edge read as one band: this is the strip of visible graph paper that tells them apart and
    /// gives each band's grip and label an uncontested edge to sit against.
    ///
    /// It is deliberately <see cref="BandTypes.GridMinorMm"/> — the drag snap — and not some
    /// smaller cosmetic value. Every band below the first is displaced by a whole number of these
    /// gaps, so a band's canvas origin stays on the lattice and the element snapping in
    /// ReportDesigner.razor.js (which converts band-relative Y through page space against the
    /// grid's own origin) still lands on the gridlines the author can see. An off-lattice gap
    /// would silently put every band below the first out of phase with the grid.
    /// </summary>
    public const double DesignGapMm = BandTypes.GridMinorMm;

    /// <summary>
    /// Every band in print order with both its canvas Y and its printed Y. The canvas advance
    /// adds <see cref="DesignGapMm"/> on top of the band's height and the report's own gap; the
    /// printed advance does not, which is what keeps the divergence measurable instead of merely
    /// accumulating. Table bands have no stored height, so they are drawn at a nominal box the
    /// author can still select and drop a table into.
    /// </summary>
    public static IReadOnlyList<BandBox> Bands(Report report)
    {
        // The caller owns this JSON and a null Bands, or a null entry inside it, deserializes
        // straight through (see the consolidated note atop DesignPreview.Resolve). BandBox.Index
        // must stay the entry's true position in report.Bands — it round-trips through the
        // SVG's data-band-index into ReportDesigner.razor's own report.Bands[bandIndex] lookups —
        // so a null entry is skipped with `continue` rather than filtered out, which would
        // renumber every surviving band behind it.
        var bands = report.Bands ?? [];
        var boxes = new List<BandBox>(bands.Count);
        var gap = PageSheet.BandGapMm(report);
        var top = PageSheet.MarginsMm(report.Page).Top;
        var y = top;
        var printY = top;

        for (var i = 0; i < bands.Count; i++)
        {
            var band = bands[i];
            if (band is null)
                continue;
            var height = band.Type == BandType.Table ? TableBandMm : band.HeightMm;
            boxes.Add(new BandBox(band, i, y, height, printY));
            y += height + gap + DesignGapMm;
            printY += height + gap;
        }
        return boxes;
    }

    /// <summary>
    /// Converts an offset in the printed report to the canvas offset showing the same content,
    /// by adding back the designer gaps stacked above it. Takes the displacement of the last band
    /// starting at or before <paramref name="printY"/>: a position inside a band maps exactly, and
    /// one past the last band keeps that band's displacement rather than running off the mapping.
    /// </summary>
    public static double CanvasY(IReadOnlyList<BandBox> boxes, double printY)
    {
        var shift = 0d;
        foreach (var box in boxes)
        {
            if (box.PrintY > printY)
                break;
            shift = box.Y - box.PrintY;
        }
        return printY + shift;
    }

    /// <summary>A table band's drawn height in millimetres. Its real height is its row count at
    /// render time, which the designer cannot know — this is a placeholder box, not a stored
    /// value. A whole multiple of the 4mm major lattice, so a table band does not knock the
    /// bands below it onto a different parity.</summary>
    public const double TableBandMm = 24;

    /// <summary>
    /// How tall the stacked bands actually are, in millimetres: the last band's bottom edge
    /// plus the bottom page margin. The sheet may need to draw more than this for a single
    /// blank page — callers take the larger of this and the page height.
    /// </summary>
    public static double ContentHeightMm(Report report)
    {
        var boxes = Bands(report);
        var margins = PageSheet.MarginsMm(report.Page);
        return boxes.Count == 0
            ? margins.Top + margins.Bottom
            : boxes[^1].Y + boxes[^1].Height + margins.Bottom;
    }
}
