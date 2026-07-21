# Pages.Reporting

A .NET 10 component library for reporting. A report is defined once as a **single
self-contained JSON document** and can then be rendered two ways from that same JSON:

- **On screen** — Blazor components rendering exactly what the PDF will show (static bordered tables, SVG charts).
- **As a file** — PDF or Excel returned as `byte[]`.

The PDF can look exactly like the screen because it *is* the same thing: the identical
Blazor components are rendered to HTML and printed with headless Chromium, using whatever
CSS you supply.

**The library ships zero report styling.** Components render semantic markup with stable
`pr-*` class names; you style them entirely with your own CSS (see below).

## Project status

Pages.Reporting is under active development, and we want to be straightforward about where
it stands.

**This library was built with significant help from AI tooling.** We use it ourselves and
we think it holds up, but it has not yet accumulated the years of real-world use that
harden a mature reporting library. Please review the code and test it thoroughly against
your own requirements before depending on it in production.

**Features are missing, and we know it.** The roadmap is long and we are actively working
through it. If something you need isn't here, telling us about it is the fastest way to get
it prioritised.

We built this because rendering real reports in Blazor — one definition, matching on screen
and in PDF — was harder than it should have been. If that's a problem you have too, we'd
like your help.

## Contributing and feedback

Every kind of contribution is welcome, from anywhere in the world:

- **Bug reports** — open an issue with the report JSON (redact your connection strings) and
  what you expected to happen.
- **Feature requests** — tell us what you're trying to build and where the current model
  gets in the way.
- **Code and security review** — especially around the connection-string encryption and the
  SQL execution path. Critical feedback is genuinely appreciated.
- **Pull requests** — bug fixes, features, tests, documentation, or sample reports.
- **General feedback** — API ergonomics, confusing docs, or a rough edge in the designer.

You don't need permission to open an issue or a pull request, and no contribution is too
small.

## Projects

| Project | What it is |
|---|---|
| `src/Pages.Reporting.Core` | Report model, JSON contract, encryption, data resolution, SVG charts |
| `src/Pages.Reporting.Blazor` | Razor Class Library: `<ReportView>` + element components (unstyled) |
| `src/Pages.Reporting.Designer` | Drag-and-drop `<ReportDesigner>` component (isolated CSS/JS) |
| `src/Pages.Reporting.Export` | `PdfReportExporter` (Playwright/Chromium) and `ExcelReportExporter` (ClosedXML) |
| `samples/Pages.Reporting.Demo` | Blazor Web App playing the "consumer" role — run this to try everything |

## The flow

```
1. DESIGN     <ReportDesigner Json="@existingJsonOrNull" OnSave="HandleSave" />
              Left sidebar: Components tab (drag text, label+value, table, chart, KPI,
              section onto the page) and Data tab (tree of Connections, Data sets,
              Parameters — click an item to edit it in the right panel). Connections
              are per-provider forms (server, database, credentials + a Test button);
              the raw connection string is an optional advanced editor. Components
              pick a data set and its columns. No per-component SQL.
              (Or build a Report object in C# and serialize it yourself.)

2. SAVE       OnSave fires with the updated JSON string — connection strings inside are
              encrypted (enc:v1:...). YOU store the JSON wherever you want (your DB, a
              file, blob storage). The library persists nothing.

3. GENERATE   // later, hand the stored JSON back:
              UI:     <ReportView Json="@json" />
              PDF:    byte[] pdf  = await pdfExporter.ExportAsync(json, css);
              Excel:  byte[] xlsx = await excelExporter.ExportAsync(json);
```

## Data sets — one query, many components

Data comes from **named, report-level data sets**: a connection + a query (or inline
static rows), declared once and referenced by any number of components. Each data set
executes **once per generation**, no matter how many components read it.

```json
"dataSets": [
  { "name": "sales",
    "binding": { "$type": "sql", "connection": "demo",
                 "sql": "SELECT Region, Product, Quantity, Revenue FROM Sales" } }
],
"elements": [
  { "$type": "kpiCard", "label": "Total revenue", "format": "C0",
    "binding": { "$type": "dataSet", "name": "sales", "field": "Revenue", "aggregate": "sum" } },
  { "$type": "table",
    "binding": { "$type": "dataSet", "name": "sales" } }
]
```

- **Rows consumers** (table, chart) read the data set's rows directly.
- **Scalar consumers** (label+value, KPI card) read one column (`field`, i.e. *data.column*)
  reduced by an `aggregate`: `first` (default), `sum`, `average`, `count`, `min`, `max`.
- Per-element embedded queries (`"$type": "sql"` / `"inline"` bindings) from older JSON
  remain fully supported at render time; the designer offers a one-click switch to a
  data set when you select such a component.

### Text expressions

The **Text** component mixes literal text with data placeholders resolved from the
report's data sets and parameters:

```json
{ "$type": "text", "content": "Total revenue: {sales.Revenue:sum:C0} across {sales.Region:count} sales" }
```

- `{dataSet.Column}` — first row's value; `{dataSet.Column:sum}` — aggregate
  (`first`/`sum`/`avg`/`count`/`min`/`max`); `{dataSet.Column:sum:C0}` — plus a .NET
  format string. Format-only also works (`{sales.Revenue:C0}`) since aggregates are
  known keywords.
- `{@paramName}` inserts a report parameter's value; `{{` writes a literal `{`.
- Line breaks in `content` are preserved. A failed expression renders inline (⚠)
  without breaking the rest of the text.
- `"renderHtml": true` renders the resolved content as raw HTML, so author-written tags
  (`<b>`, `<span style="…">`, …) take effect — on screen and in the PDF. Only enable it
  for content you trust: the text is emitted unescaped.
- Text replaces the older `title` and `value` element types, which remain fully
  supported for existing JSON.

### Parameters

Reports can declare named parameters with defaults; data set SQL references them as
`@name`, and the host overrides them at generation time — a natural fit for URL query
values:

```json
"parameters": [ { "name": "minRevenue", "defaultValue": "0" } ],
"dataSets":   [ { "name": "topSales", "binding": { "$type": "sql", "connection": "demo",
                  "sql": "SELECT * FROM Sales WHERE Revenue >= @minRevenue" } } ]
```

```csharp
var values = new Dictionary<string, string?> { ["minRevenue"] = "5000" }; // e.g. from the URL
<ReportView Json="@json" Parameters="values" />
byte[] pdf = await pdfExporter.ExportAsync(json, parameters: values);
```

Only declared parameters are applied (unknown names are ignored), and a parameter is
bound to a query only when its `@name` appears in that SQL. Values travel as strings;
numeric-looking values are bound as numbers. The demo's `/reports/1?minRevenue=5000`
shows the URL flow end-to-end, including the PDF/Excel download links.

## Styling — bring your own CSS

Components emit only structure. Two hooks:

1. **Stable class names** you can target globally:

   | Class | Element |
   |---|---|
   | `pr-report` (+ `pr-print` when exporting) | report root (a 12-column CSS grid) |
   | `pr-cell` | grid cell wrapping each element |
   | `pr-page-frame` | print only: per-page frame carrying the page border/background |
   | `pr-page-break` | screen only: faint guide line where the PDF starts a new page |
   | `pr-text` | text block with `{…}` expressions |
   | `pr-title` | title header (`h1` + `p`) — legacy |
   | `pr-value`, `pr-label`, `pr-figure` | single value row — legacy |
   | `pr-kpi`, `pr-kpi-value`, `pr-caption` | KPI card (structural card look built in — screen matches designer and PDF) |
   | `pr-chart` | chart figure (`figcaption` + inline SVG) |
   | `pr-table-block`, `pr-table-title`, `pr-table` | table (static: full width, all cells bordered, all rows — screen matches PDF) |
   | `pr-left` / `pr-center` / `pr-right` | cell alignment |
   | `pr-section`, `pr-row` / `pr-column`, `pr-section-body` | layout sections |
   | `pr-error`, `pr-error-inline`, `pr-loading` | states |

2. **Per-component `cssClass`** — every element has a `cssClass` property (editable in the
   designer's properties panel) appended to that component's root, so you can style one
   specific table or KPI without touching the others.

3. **Built-in style options** — every element (and the page itself, via the designer's
   **Page** button) has an optional `style` block carried in the JSON and rendered inline,
   so it prints identically in the PDF:

   ```json
   "style": { "bold": true, "fontSize": 20, "align": "center", "color": "#1a66c2",
              "background": "#f5f5f5", "padding": "8px 16px",
              "borderWidth": 2, "borderColor": "#1a66c2",
              "borderTop": true, "borderBottom": true }
   ```

   Borders are structured — pick the sides (`borderTop`/`borderRight`/`borderBottom`/
   `borderLeft`), a width, and a color — or set the free-form `"border": "1px dashed #888"`
   shorthand, which overrides the structured fields.

   All fields optional; your own CSS still wins whenever you want full control.

4. **12-column grid layout** — the report body is a 12-column grid (structural, so it
   renders identically in the PDF with no CSS). Every element has an optional
   `columnSpan` (1–12, omitted = full width) carried in the JSON:

   ```json
   { "$type": "kpiCard", "label": "Revenue", "columnSpan": 4, ... }
   ```

   Two consecutive `columnSpan: 6` elements sit side by side. An optional `minHeight`
   ("200", "60mm") sets the cell's *minimum* height — content can still grow, and in a
   side-by-side row the tallest value sets the row height. An optional `rowSpan` lets a
   tall component sit beside a vertical stack: order the elements A (`rowSpan: 2`), B1,
   C (`rowSpan: 2`), B2 — grid auto-placement fills free slots left to right, so B2
   lands under B1. In the designer, select a
   control and snap it to grid lines with the `◂ 6/12 ▸` stepper on its badge or the
   Width/Height inputs in the properties panel; grid lines show while dragging.
   Row sections without explicit spans still split their row evenly, and a section can
   set `"columns": N` (designer: Layout → Custom grid…) to place N children per row,
   wrapping into rows automatically. Report-level `"spacing": "row column"` (default
   `"12 16"`, designer: Page → Component spacing) controls the gap between components;
   `"0"` makes boxes touch — use per-side borders to avoid doubled lines at shared edges.

Charts additionally read CSS variables (`--pr-series-1…8`, `--pr-grid`, `--pr-muted`,
`--pr-surface`, `--pr-baseline`, `--pr-ink-secondary`) with sensible built-in fallback
colors, so they work unstyled and are themeable when you want.

The PDF has no footer by default, so its printable area matches the on-screen page
exactly. Set the page's `"showFooter": true` (designer: Page → PDF footer) to print the
report name + page numbers on every page — this reserves extra bottom margin, so long
content paginates slightly earlier than the screen suggests.

On screen the report is one continuous sheet — it does not paginate. So a long report
still shows where the PDF will break: `ReportView` draws a faint guide line
(`pr-page-break`) at every PDF page boundary, computed from the same page size, print
margin, page padding, and footer reserve the exporter uses. It's approximate — Chromium
moves a component that doesn't fit (a chart, a KPI card) wholly onto the next page,
which shifts later breaks down. Hide it with `.pr-page-break { display: none; }`.

For **PDF**, pass the same CSS you use on screen:

```csharp
builder.Services.AddPagesReportingExport(o => o.Css = myReportCss);   // default
byte[] pdf = await pdfExporter.ExportAsync(json, cssForThisExport);    // per-call override
```

## Setup in a consuming app

```csharp
builder.Services.AddPagesReporting();          // + options => options.EncryptionKey = "..."
builder.Services.AddPagesReportingExport();    // only if you need PDF/Excel
```

The designer needs no extra registration — it uses the same services. Its own chrome CSS
ships via Blazor scoped-CSS isolation (your app's `*.styles.css` bundle) and its only JS
is an isolated collocated module for the HTML5 drag handshake.

`<ReportDesigner>` is self-contained — palette, canvas, properties, preview, save — and
fills the box you place it in (give the wrapper a height, e.g. `calc(100dvh - <your
chrome>)`); every panel scrolls internally, so the page itself never needs to scroll.
Pass `Json` if editing an existing report; `OnSave` hands back the new report JSON.

### Connection-string encryption

AES-256-GCM. The key is resolved in order:

1. `AddPagesReporting(o => o.EncryptionKey = ...)` — base64 of 32 bytes, or any passphrase
2. `PAGES_REPORTING_KEY` environment variable
3. an auto-generated `pages-reporting.key` file next to the app (created on first use)

Reports encrypted with one key can only be generated where that key is available.
Supported databases — **SQL Server, SQLite, PostgreSQL, MySQL** — ship with the library;
consuming apps install no ADO.NET packages.

### PDF prerequisites

`PdfReportExporter` uses Playwright's Chromium (~150 MB, one-time). It downloads
automatically on the first export, or pre-install with:

```
pwsh bin/Debug/net10.0/playwright.ps1 install chromium
```

## Running the demo

```
cd samples/Pages.Reporting.Demo
dotnet run
```

The demo seeds a SQLite database with sample sales data and one stored report.
"+ New report" opens the drag-and-drop designer; reports render deliberately unstyled
(the demo ships no report CSS) to prove the bring-your-own-CSS contract.
