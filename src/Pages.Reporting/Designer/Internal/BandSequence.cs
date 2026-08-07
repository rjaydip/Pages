using Pages.Reporting.Core.Model;

namespace Pages.Reporting.Designer.Internal;

/// <summary>
/// Checks the band list for group arrangements that will not do what the author means. These
/// are warnings, never errors: a malformed report still prints, with the offending band
/// rendered once and ungrouped, so the banner is the only signal there is.
/// </summary>
internal static class BandSequence
{
    public static IReadOnlyList<string> Problems(Report report)
    {
        // Margin bands are excluded here for the same reason BandPlan excludes them: they
        // never take part in a group.
        var bands = report.Bands.Where(band => !BandTypes.IsMargin(band.Type)).ToList();
        var problems = new List<string>();
        Walk(bands, 0, bands.Count, problems);
        return problems;
    }

    /// <summary>
    /// Mirrors BandPlan.Expand deliberately: a header owns the span up to the first footer at
    /// its OWN level, and anything it cannot pair with is an orphan. Validating with a
    /// different model than the renderer uses is how a banner ends up condemning an
    /// arrangement that prints perfectly well.
    /// </summary>
    private static void Walk(List<PageBand> bands, int start, int end, List<string> problems)
    {
        for (var i = start; i < end; i++)
        {
            var band = bands[i];

            // Reached in the generic branch, exactly as in Expand — no header opened this one.
            if (band.Type == BandType.GroupFooter)
            {
                problems.Add($"{Name(band)} has no group header before it — it will print once, ungrouped.");
                continue;
            }

            if (band.Type != BandType.GroupHeader)
                continue;

            if (band.Group is null || string.IsNullOrWhiteSpace(band.Group.DataSet)
                || string.IsNullOrWhiteSpace(band.Group.Field))
                problems.Add($"{Name(band)} has no data set and field to group by — it will print once, ungrouped.");

            var footer = FindFooter(bands, i, end, band.Level);
            if (footer < 0)
            {
                problems.Add($"{Name(band)} has no group footer at level {band.Level} after it — it repeats once per value, but encloses nothing.");
                continue;
            }

            Walk(bands, i + 1, footer, problems);
            i = footer;
        }
    }

    /// <summary>The matching footer's index within this span, or -1 — the same rule BandPlan uses.</summary>
    private static int FindFooter(List<PageBand> bands, int headerIndex, int end, int level)
    {
        for (var i = headerIndex + 1; i < end; i++)
            if (bands[i].Type == BandType.GroupFooter && bands[i].Level == level)
                return i;
        return -1;
    }

    private static string Name(PageBand band) =>
        string.IsNullOrWhiteSpace(band.Name) ? BandLabels.Label(band.Type) : $"{BandLabels.Label(band.Type)} \"{band.Name}\"";
}
