# Roadmap

What we intend to build. Not a commitment or a schedule — priorities move, and a feature request
with a real use case behind it moves them fastest. Shipped work is recorded in
[CHANGELOG.md](CHANGELOG.md).

## Already shipped

Worth stating, so nothing here is rebuilt by accident:

- The report is a list of **bands** (`Report.Bands`) — report title, page header/footer, group
  header/footer, content, table and report summary — with elements positioned absolutely in
  **millimetres** (`LayoutPosition`) inside each band.
- **Grouping.** A `GroupHeader` band with a `GroupBinding`, paired to its `GroupFooter` by
  `Level`, with per-group aggregates.
- **Built-in element styling** — `ElementStyle` (bold/italic, font, size, colour, background,
  padding, alignment, borders), rendered inline so the PDF matches the screen.
- **Built-in text tokens** — `{page}`, `{pages}`, `{reportName}`, `{now:…}` — alongside
  `{dataSet.Column:aggregate:format}` and `{@param}` expressions.
- Table columns take a **.NET format string** (`"N2"`, `"C"`, `"d"`), an alignment, and an
  aggregate (`ColumnDefinition.Format` / `Align` / `Aggregate`); the table renders a real
  `<thead>` that Chromium repeats on every printed page.
- Charts cover **bar, line and pie**. Report **parameters with defaults**, overridable at
  generation time. An **SVG designer** showing bands to scale.
- **Connection strings supplied at runtime** — a connection marked `suppliedAtRuntime` takes
  its string from `ReportRuntimeOptions.ConnectionStrings` at generation time, so one
  definition runs against dev/staging/prod or a per-tenant database *(0.3.0)*.
- Everything in **one package** (`Pages.Reporting`), PDF via Chromium and Excel via ClosedXML.

## Planned

Ordered by how much it is asked for and how central it is to real reporting work — see
[the release grouping](#release-grouping) below for how these are expected to land.

| Priority | Feature | Group |
|---|---|---|
| 1 | Conditional / per-column table formatting | Tables |
| 2 | Adjustable column widths + row/header heights | Tables |
| ~~3~~ | ~~Connection strings supplied at runtime~~ — shipped 0.3.0 | Data |
| 4 | `ImageElement` (static + per-row) | Content |
| 5 | Typed parameters | Data |
| 6 | Culture-aware formatting | Tables |
| 7 | Designer undo/redo, copy/paste, multi-select, guides | Designer |
| 8 | More runtime variables and functions | Data |
| 9 | CSV and HTML export | Output |
| 10 | Barcodes and QR codes | Content |
| 11 | More chart types + axis/legend/colour control | Output |
| 12 | Sub-reports | Designer |
| 13 | Watermarks + page-range export | Output |

### Tables

- **Richer column formatting** *(priority 1)*. Beyond the format string: per-column style (font,
  colour, background), and **conditional formatting** — colour a cell or a whole row from an
  expression, e.g. negative values in red, overdue rows shaded. This is the single most-asked-for
  feature in reporting tools and the model has no equivalent today.
- **Adjustable header and row height, and column widths** *(priority 2)*. Column width is the gap
  that bites first: today the table is full-width with even columns, so a long description column
  cannot be given more room than a date column.
- **Culture-aware formatting** *(priority 6)*. `ValueFormatter` formats invariantly, so `"C"`
  cannot produce `₹` or `€`, and dates cannot follow a locale. A report-level culture would fix
  both.

### Data

- ~~**Connection strings supplied at runtime** *(priority 3)*~~ — **shipped 0.3.0.** A connection
  marked `suppliedAtRuntime` takes its string from `ReportRuntimeOptions.ConnectionStrings` at
  generation time, so one definition runs against dev, staging and production, or a per-tenant
  database, without duplicating it. See [the report definition](report-definition.md#connections-at-runtime).
- **Typed parameters** *(priority 5)*. `ParameterDefinition` is a name and a string default. A
  type (number, date, boolean, or a list of allowed values) would drive real inputs in
  `<ReportParameters>` instead of a text box, and let the designer validate before running a
  query.
- **More runtime variables and functions** *(priority 8)*. Built-ins beyond page numbers —
  today's date, user name, row index, running totals — and expression functions in text
  templates: arithmetic, string operations, conditionals, date maths.
- **More data sources** *(unprioritised)*. REST/JSON APIs, and CSV or in-memory collections for
  reports whose data the host already has. The `DataBinding` model is the extension point; the
  resolver is the part that assumes ADO.NET.

### Content

- **Images** *(priority 4)*. An `ImageElement` — static assets, and images from a data set (a
  logo per branch, a product photo per row). Needs a decision on how bytes reach the PDF: a URL
  Chromium can fetch, or base64 embedded in the JSON so the report stays self-contained. The
  self-contained rule argues for embedding, with a size limit.
- **Barcodes and QR codes** *(priority 10)*. Invoices, labels, and picking lists need them, and
  today there is no way to produce one. Renders as SVG so it stays sharp in the PDF, like the
  charts do.

### Output

- **CSV and HTML export** *(priority 9)*, alongside PDF and Excel.
- **More chart types** *(priority 11)*, and control over axes, legends and colours — stacked
  bars, area, donut, scatter.
- **Watermarks** *(priority 13)*, and page-range export.

### Designer

- **Undo/redo, copy/paste, multi-select, alignment guides** *(priority 7)*. The canvas is usable
  but unforgiving: a mis-drag cannot be undone, and every element must be positioned one at a
  time. This is the least glamorous item here and probably the one that most affects whether the
  tool is pleasant.
- **Sub-reports** *(priority 12)* — embedding one report inside another's band, for statements
  and invoices with repeating detail sections.

## Release grouping

How the planned work is expected to land. Themed so a release is coherent rather than one item
from each group; order and contents move with demand.

- **0.3.0 — "Data at runtime"** — connection strings supplied at generation time (3, **done**),
  typed parameters (5), and more runtime variables and functions (8). Mostly additive.
- **0.4.0 — "Tables"** — conditional and per-column formatting (1), column widths and row/header
  heights (2), culture-aware formatting (6). The heaviest group, so it gets its own release.
- **0.5.0 — "Content & output"** — `ImageElement` (4), barcodes and QR codes (10), CSV and HTML
  export (9).
- **Ongoing** — designer polish (7) folded into whichever release has room; charts (11),
  sub-reports (12) and watermarks (13) unscheduled.

## Under consideration

Real, but not obviously worth the weight yet:

- Splitting PDF export into its own package, so consumers who never export do not carry
  Playwright's ~195 MB.
- Accessible/tagged PDF output (PDF/UA), which some public-sector work requires.
- Streaming or paged data resolution, for reports over very large result sets.
- Theme presets, so a report can start from a look rather than from nothing.
