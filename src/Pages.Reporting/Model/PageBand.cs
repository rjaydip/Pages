namespace Pages.Reporting.Core.Model;

/// <summary>
/// The kinds of band a report is built from. A band's type decides everything about it:
/// where it sits, how often it prints, and — for <see cref="Table"/> — whether it sizes
/// itself from its data instead of its declared height.
/// </summary>
public enum BandType
{
    /// <summary>
    /// Prints once, wherever it sits in the band list — conventionally first, but list
    /// order is the truth: nothing pins it to the top.
    /// </summary>
    ReportTitle,

    /// <summary>Every page, printed inside the top page margin.</summary>
    PageHeader,

    /// <summary>Once per distinct value of its <see cref="PageBand.Group"/> binding's column; opens a group.</summary>
    GroupHeader,

    /// <summary>Free canvas for text and charts. Prints once, in list order.</summary>
    Content,

    /// <summary>Holds one table; the only band that grows with its data.</summary>
    Table,

    /// <summary>Once per group, after its rows; closes the header with the same <see cref="PageBand.Level"/>.</summary>
    GroupFooter,

    /// <summary>Every page, printed inside the bottom page margin.</summary>
    PageFooter,

    /// <summary>
    /// Prints once, wherever it sits in the band list. Reaches the bottom of the last page
    /// only when it is genuinely the last band the body prints (see
    /// <see cref="BandTypes.PinsToBottom"/>); moved anywhere else, it prints in place like
    /// any other band.
    /// </summary>
    ReportSummary,
}

/// <summary>
/// One band of the report. Bands are the whole report — <see cref="Report.Bands"/> in list
/// order — and each is a fixed-height canvas whose elements carry an absolute
/// <see cref="ReportElement.Position"/>.
/// </summary>
public sealed class PageBand
{
    public string? Id { get; set; }

    /// <summary>Label shown in the designer; never rendered.</summary>
    public string? Name { get; set; }

    public BandType Type { get; set; } = BandType.Content;

    /// <summary>
    /// Nesting depth for group bands; 1 is outermost. A <see cref="BandType.GroupFooter"/>
    /// closes the <see cref="BandType.GroupHeader"/> with the same level, so a level-2 group
    /// opened inside a level-1 one partitions that group's rows further. Ignored by every
    /// other band type.
    /// </summary>
    public int Level { get; set; } = 1;

    /// <summary>
    /// The partition a <see cref="BandType.GroupHeader"/> opens. Null on every other band
    /// type — including the matching footer, which is paired by <see cref="Level"/>.
    /// </summary>
    public GroupBinding? Group { get; set; }

    /// <summary>
    /// The band's fixed height in millimetres — the box its elements are positioned in.
    /// Content taller than this is clipped. Ignored by <see cref="BandType.Table"/>, which
    /// takes its height from its rows. 16mm is four cells of the 4mm lattice, so a stack of
    /// default bands puts every band edge on a dark line.
    /// </summary>
    public double HeightMm { get; set; } = 16;

    /// <summary>Optional CSS class(es) on the band's root markup, beside the stable pr-band* names.</summary>
    public string? CssClass { get; set; }

    public ElementStyle? Style { get; set; }

    public List<ReportElement> Elements { get; set; } = [];
}

/// <summary>Band-type predicates shared by the renderer, the exporters and the designer.</summary>
public static class BandTypes
{
    /// <summary>
    /// Printed inside the page margin on every page. Chromium sizes the margin before it
    /// paginates, so these bands' heights are reserved up front and they never flow with
    /// the body.
    /// </summary>
    public static bool IsMargin(BandType type) =>
        type is BandType.PageHeader or BandType.PageFooter;

    /// <summary>Sits above the content it belongs to, rather than below it.</summary>
    public static bool IsTop(BandType type) =>
        type is BandType.ReportTitle or BandType.PageHeader or BandType.GroupHeader;

    /// <summary>
    /// A Table band holds exactly one <see cref="TableElement"/> — full, empty, or already
    /// holding its one table are all fine; anything else would strand extra content that
    /// never renders (the Table branch only ever shows the first TableElement it finds).
    /// </summary>
    public static bool CanHoldTable(PageBand band) =>
        band.Elements.Count == 0 || (band.Elements.Count == 1 && band.Elements[0] is TableElement);

    /// <summary>
    /// List order is the truth — a band prints where it sits, and the author is responsible
    /// for placement. A report summary reaches the bottom of the last page only when it is
    /// genuinely the last band the body prints; moved anywhere else, it prints in place like
    /// any other band. (Margin bands don't occupy the body, so they don't count as "last".)
    /// </summary>
    public static bool PinsToBottom(Report report, PageBand band) =>
        band.Type == BandType.ReportSummary && ReferenceEquals(LastBodyBand(report), band);

    /// <summary>True when the report's last body band is a report summary — the one case
    /// where the body needs to stretch to the page so that summary's pin can reach the edge.</summary>
    public static bool HasPinnedSummary(Report report) =>
        LastBodyBand(report)?.Type == BandType.ReportSummary;

    private static PageBand? LastBodyBand(Report report) =>
        report.Bands.LastOrDefault(band => !IsMargin(band.Type));

    /// <summary>
    /// The designer's alignment lattice in millimetres: a dark line every 4mm, a light one
    /// every 2mm, and 2mm is the drag snap. Fixed rather than derived from the page, because a
    /// lattice computed as a fraction of the content width never lands on whole numbers — which
    /// is what put values like 260.22 into stored reports. At 100% zoom a minor line falls every
    /// 7.6 device pixels; finer than that the lattice reads as a grey wash rather than a grid.
    /// </summary>
    public const double GridMajorMm = 4;

    /// <summary>
    /// The light line, and the step a drag snaps to — the author aims at a line they can see, so
    /// the snap is deliberately tied to the finest drawn one. Alt bypasses snapping, and typed
    /// input still accepts half-millimetres.
    /// </summary>
    public const double GridMinorMm = 2;

    /// <summary>
    /// The shortest a band may be, in millimetres. A whole multiple of the snap, so the drag
    /// clamp cannot land a band on a height the lattice has no line for. Enforced on the resize
    /// grip and normalised on typed input, so the two cannot disagree about what is legal.
    /// </summary>
    public const double MinBandHeightMm = 6;

    /// <summary>
    /// An element's default rectangle when it is added without one — a readable box near the
    /// band's top-left, staggered by how many elements already sit in the band so successive
    /// drops don't land exactly on top of each other. Every number is a whole multiple of the
    /// 2mm snap, and the bottom edge (2 + 10 = 12mm) clears the 16mm default band, so a freshly
    /// dropped element is never born already clipped.
    /// </summary>
    public static LayoutPosition DefaultPosition(int existingCount) => new()
    {
        X = 4 + existingCount % 3 * 24,
        Y = 2,
        Width = 60,
        Height = 10,
    };
}
