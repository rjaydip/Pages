namespace Pages.Reporting.Core.Model;

/// <summary>
/// An element's rectangle inside a band, in millimetres from the band's top-left.
/// Millimetres because paper is defined in them — A4 is exactly 210×297mm, where the same
/// page in points is 595.2755…×841.8897…, and no grid divides that cleanly. CSS understands
/// mm directly and Chromium converts it for the PDF, so the stored number is still the drawn
/// number and the printed number, with no conversion anywhere. Absolute (not percent-of-band)
/// so resizing a band never resizes its contents.
/// </summary>
public sealed class LayoutPosition
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
}
