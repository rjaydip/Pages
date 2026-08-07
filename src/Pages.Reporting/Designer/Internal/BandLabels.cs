using Pages.Reporting.Core.Model;

namespace Pages.Reporting.Designer.Internal;

/// <summary>Display names for band types, and the set the toolbar offers.</summary>
internal static class BandLabels
{
    /// <summary>
    /// Band types the "+ Add band" menu offers, in the order they usually appear in a
    /// report. Group header/footer print once per distinct value of a data set column —
    /// see <see cref="BandSequence.Problems"/> for the pairing rules the designer checks.
    /// </summary>
    public static readonly BandType[] Addable =
    [
        BandType.ReportTitle,
        BandType.PageHeader,
        BandType.GroupHeader,
        BandType.Content,
        BandType.Table,
        BandType.GroupFooter,
        BandType.PageFooter,
        BandType.ReportSummary,
    ];

    public static string Label(BandType type) => type switch
    {
        BandType.ReportTitle => "Report title",
        BandType.PageHeader => "Page header",
        BandType.GroupHeader => "Group header",
        BandType.Content => "Content",
        BandType.Table => "Table",
        BandType.GroupFooter => "Group footer",
        BandType.PageFooter => "Page footer",
        BandType.ReportSummary => "Report summary",
        _ => "Band",
    };
}
