# Styling — bring your own CSS

The library ships **zero report styling**. Components emit only structure; you style them
entirely with your own CSS.

- [The report definition](report-definition.md) — data sets, text expressions, parameters
- [Bands and groups](bands-and-groups.md) — the page structure

## 1. Stable class names

| Class | Element |
|---|---|
| `pr-report` (+ `pr-print` when exporting) | report root — a flex column holding the body, and a bottom-pinned `reportSummary` after it |
| `pr-body` | the report body — the ordered band stack, in normal block flow |
| `pr-band` | every band, plus a type-derived class — lower-cased `type`, e.g. `pr-band-content`, `pr-band-table`, `pr-band-pageheader` |
| `pr-band-header` / `pr-band-footer` | only on `pageHeader` / `pageFooter` — the two bands that print in the page margin |
| `pr-band-body`, `pr-band-el` | a band's positioned-element wrapper and each element's wrapper (not present on a `table` band, which holds its one table directly) |
| `pr-cell` | grid cell wrapping each child of a section |
| `pr-page-frame` | print only: per-page frame carrying the page border/background |
| `pr-page-break` | screen only: faint guide line where the PDF starts a new page |
| `pr-text` | text block with `{…}` expressions |
| `pr-chart` | chart figure (`figcaption` + inline SVG) |
| `pr-table-block`, `pr-table-title`, `pr-table` | table (static: full width, all cells bordered, all rows — screen matches PDF) |
| `pr-left` / `pr-center` / `pr-right` | cell alignment |
| `pr-section`, `pr-row` / `pr-column`, `pr-section-body` | layout sections |
| `pr-error`, `pr-loading` | states |
| `pr-params`, `pr-param`, `pr-param-label`, `pr-param-input`, `pr-param-select`, `pr-param-check`, `pr-params-apply` | `<ReportParameters>` — the optional runtime parameter form. `pr-param-input` covers the text / number / date boxes (distinguish via `[type=number]` / `[type=date]`); `pr-param-select` is the list dropdown; `pr-param-check` is the boolean checkbox. Not part of the report itself, so it never appears in a PDF |
| `pr-param-warning` | a report-level banner when a runtime parameter value did not match its declared type |

## 2. Per-component `cssClass`

Every element has a `cssClass` property (editable in the designer's properties panel) appended
to that component's root, so you can style one specific table or chart without touching the
others.

## 3. Built-in style options

Every element — and the page itself, via the designer's **Page** button — has an optional
`style` block carried in the JSON and rendered inline, so it prints identically in the PDF:

```json
"style": { "bold": true, "fontSize": 20, "fontFamily": "Georgia, 'Times New Roman', serif",
           "align": "center", "color": "#1a66c2",
           "background": "#f5f5f5", "padding": "8px 16px",
           "borderWidth": 2, "borderColor": "#1a66c2",
           "borderTop": true, "borderBottom": true }
```

Borders are structured — pick the sides (`borderTop`/`borderRight`/`borderBottom`/`borderLeft`),
a width, and a color — or set the free-form `"border": "1px dashed #888"` shorthand, which
overrides the structured fields.

`fontFamily` takes a CSS font stack, not a single name. The PDF is printed by Chromium on the
**server**, so a font only appears there if it is installed on that machine or delivered by your
own CSS via `@font-face` — otherwise the stack falls back to the next entry, which is why every
font the designer offers ends in a generic family.

The Excel export takes the first concrete family from that stack (generic families and
`-apple-system`-style keywords are skipped), because a spreadsheet cell holds one font name and
no fallbacks. Substitution there happens on the machine **opening** the workbook rather than on
the server, so a font your readers do not have is silently swapped by Excel.

The `style` block is rendered as inline CSS and is not sanitized — `border`, `padding` and
`fontFamily` all accept free-form CSS, and a report definition is trusted input: anyone who can
save one can put arbitrary CSS into every page that renders it, including the server-side
Chromium that prints the PDF. If your app lets end users author reports, gate who may save one.

All fields optional; your own CSS still wins whenever you want full control.

## 4. Band canvas and the section grid

The report body is an ordered list of bands. Every band is a fixed-height canvas: each element
carries a `position` (`x`/`y`/`width`/`height`) — a rectangle in millimetres from the band's
top-left, absolute, so resizing a band never resizes its contents — and you drag it anywhere in
the designer. A `table` band is the exception — it holds one table and grows to its row count,
so it never clips.

```json
{ "$type": "text", "content": "Revenue", "position": { "x": 0, "y": 0, "width": 56, "height": 32 } }
```

Inside a **section**, children still use the 12-column grid: `columnSpan` (1–12, omitted = full
width), `minHeight` ("200", "60mm") for a minimum cell height, and `rowSpan` to let a tall
component sit beside a vertical stack. Row sections without explicit spans split their row
evenly, and a section can set `"columns": N` (designer: Layout → Custom grid…) to place N
children per row. Report-level `"spacing": "row column"` (default `"12 16"`, designer: Page →
Section spacing) controls the gap between a section's children.

## Charts

Charts read CSS variables (`--pr-series-1…8`, `--pr-grid`, `--pr-muted`, `--pr-surface`,
`--pr-baseline`, `--pr-ink-secondary`) with sensible built-in fallback colors, so they work
unstyled and are themeable when you want.

## What the design canvas shows

The design canvas draws each band at its exact size, but it separates consecutive bands by a
2mm strip of visible paper so you can tell them apart and grab each one's edge. That spacing is
an editing aid only — it is not in your report and does not print, and the canvas's page-break
guide already accounts for it.

Each element draws **its own content, with its own styling, unresolved**: the canvas renders
through the same components the PDF does, so font size, family, weight, alignment, colour,
background and borders are what you will get. A text element shows its template literally —
`{sales.Revenue:sum:C0}`, not a value — because the designer never runs your queries. A table
shows its header row plus one row naming each column, at real column widths. A chart shows its
"No data" placeholder at its configured height. An element with nothing authored yet keeps a
small type label so it stays findable.

Three things deliberately differ from the printed page, all consequences of there being no data
or no page:

- A column set to `align: auto` previews left-aligned, and a `count` aggregate previews as `1`,
  because every preview value is a placeholder string.
- Band `style` background, border and padding are not drawn. Padding matters most: in print it
  shifts every element in that band, so a band laid out perfectly on the canvas can move on
  export.
- Page-level `style` padding, border and background are likewise not drawn, so bands preview
  wider than they print.

Page and band `style` *text* settings — font size, family, weight, colour, alignment — do preview,
since they inherit into the elements exactly as they do in print.

## CSS for the PDF

Pass the same CSS you use on screen:

```csharp
builder.Services.AddPagesReportingExport(o => o.Css = myReportCss);   // default
byte[] pdf = await pdfExporter.ExportAsync(json, cssForThisExport);    // per-call override
```
