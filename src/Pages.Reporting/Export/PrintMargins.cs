namespace Pages.Reporting.Export;

/// <summary>
/// Formats millimetre values for the PDF page setup. Geometry is stored in millimetres and
/// the page setup takes millimetres, so nothing converts here any more — this is only the
/// invariant number formatting, kept in one place so the exporter's strings cannot drift.
/// </summary>
internal static class PrintMargins
{
    public static string Mm(double value) => FormattableString.Invariant($"{value:0.###}mm");
}
