# Roadmap

What we intend to build. Not a commitment or a schedule — priorities move, and a feature request
with a real use case behind it moves them fastest. Shipped work is recorded in
[CHANGELOG.md](CHANGELOG.md).

## Already shipped

Worth stating, so nothing here is rebuilt by accident:

- Table columns already take a **.NET format string** (`"N2"`, `"C"`, `"d"`), an alignment, and an
  aggregate (`ColumnDefinition.Format` / `Align` / `Aggregate`).
- Charts already cover **bar, line and pie**.
- **Page-number tokens**, **group header/footer bands**, and **report parameters with defaults**
  all work today.
- The table renders a real `<thead>`, which Chromium repeats on every printed page — so
  multi-page table headers are already handled.

## Planned

### Content

- **Images.** An `ImageElement` — static assets, and images from a data set (a logo per branch, a
  product photo per row). Needs a decision on how bytes reach the PDF: a URL Chromium can fetch,
  or base64 embedded in the JSON so the report stays self-contained. The self-contained rule
  argues for embedding, with a size limit.
- **Barcodes and QR codes.** Invoices, labels, and picking lists need them, and today there is no
  way to produce one. Renders as SVG so it stays sharp in the PDF, like the charts do.

### Tables

- **Adjustable header and row height, and column widths.** Column width is the gap that bites
  first: today the table is full-width with even columns, so a long description column cannot be
  given more room than a date column.
- **Richer column formatting.** Beyond the format string: per-column style (font, colour,
  background), and **conditional formatting** — colour a cell or a whole row from an expression,
  e.g. negative values in red, overdue rows shaded. This is the single most-asked-for feature in
  reporting tools and the model has no equivalent today.
- **Culture-aware formatting.** `ValueFormatter` formats invariantly, so `"C"` cannot produce
  `₹` or `€`, and dates cannot follow a locale. A report-level culture would fix both.

### Data

- **Connection strings supplied at runtime.** Today a connection lives encrypted in the report
  JSON. Passing one at generation time — the way parameters already are — lets one report run
  against dev, staging and production, or against a per-tenant database, without duplicating the
  definition.
- **More data sources.** REST/JSON APIs, and CSV or in-memory collections for reports whose data
  the host already has. The `DataBinding` model is the extension point; the resolver is the part
  that assumes ADO.NET.
- **More runtime variables and functions.** Built-ins beyond page numbers — today's date, user
  name, row index, running totals — and expression functions in text templates: arithmetic,
  string operations, conditionals, date maths.
- **Typed parameters.** `ParameterDefinition` is a name and a string default. A type (number,
  date, boolean, or a list of allowed values) would drive real inputs in `<ReportParameters>`
  instead of a text box, and let the designer validate before running a query.

### Output

- **More chart types**, and control over axes, legends and colours — stacked bars, area, donut,
  scatter.
- **CSV and HTML export**, alongside PDF and Excel.
- **Watermarks**, and page-range export.

### Designer

- **Undo/redo, copy/paste, multi-select, alignment guides.** The canvas is usable but unforgiving:
  a mis-drag cannot be undone, and every element must be positioned one at a time. This is the
  least glamorous item here and probably the one that most affects whether the tool is pleasant.
- **Sub-reports** — embedding one report inside another's band, for statements and invoices with
  repeating detail sections.

## Under consideration

Real, but not obviously worth the weight yet:

- Splitting PDF export into its own package, so consumers who never export do not carry
  Playwright's ~195 MB.
- Accessible/tagged PDF output (PDF/UA), which some public-sector work requires.
- Streaming or paged data resolution, for reports over very large result sets.
- Theme presets, so a report can start from a look rather than from nothing.
