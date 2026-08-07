# Bands and groups

A report *is* its bands: an ordered list of regions, each a fixed-height canvas holding any
report elements — text, tables, charts, sections. In the designer, **+ Add band** adds one and
you drag components onto it; ▲/▼ on a band strip reorders the report.

- [The report definition](report-definition.md) — data sets, text expressions, parameters
- [Styling](styling.md) — CSS hooks and the `style` block

```json
"bands": [
  { "type": "reportTitle", "heightMm": 18,
    "elements": [ { "$type": "text", "content": "{reportName}", "style": { "bold": true },
                    "position": { "x": 4, "y": 2, "width": 126, "height": 14 } } ] },
  { "type": "pageHeader", "heightMm": 10,
    "elements": [ { "$type": "text", "content": "Page {page} of {pages}" } ] },
  { "type": "content", "heightMm": 40, "elements": [ … ] },
  { "type": "table", "elements": [ { "$type": "table", "binding": { "$type": "dataSet", "name": "sales" } } ] },
  { "type": "reportSummary", "heightMm": 12,
    "elements": [ { "$type": "text", "content": "Approved by ______________" } ] }
]
```

**`type`** decides how a band prints — but for every type except `pageHeader`/`pageFooter`,
**list order is the truth**: a band prints where it sits, and you're responsible for its
placement.

| `type` | Prints |
|---|---|
| `reportTitle` | once, wherever it sits — conventionally first |
| `pageHeader` | every page, inside the top page margin |
| `groupHeader` | once per distinct value of its `group.field`; opens a group |
| `content` | once, in list order — a free canvas for text and charts |
| `table` | once, in list order — one table, height from its rows |
| `groupFooter` | once per group, after its rows; closes the header with the same `level` |
| `pageFooter` | every page, inside the bottom page margin |
| `reportSummary` | once, wherever it sits — reaches the bottom of the *last* page only when it is genuinely the last band in the list; moved anywhere else, it just prints in place like any other band |

## Geometry

Geometry is **millimetres** throughout — band heights, the band gap, and every element
rectangle. Paper is defined in millimetres (A4 is exactly 210×297mm), so the designer's 4mm
grid divides the printable area exactly and stored values stay whole numbers. The designer
snaps drags to 2mm — the light grid line — and accepts half-millimetres when you type a value;
hold Alt while dragging to place something off the lattice.

Bands **abut by default**: there is no gap between them, because a band is already a box you
position inside. Space before or after a component is space you left in the band, and it
prints exactly as you placed it. Set `bandGapMm` on the report only if you want a gap you did
not place.

`pageHeader` / `pageFooter` print inside the page margin, which Chromium sizes before it
paginates — so their `heightMm` is reserved on every page and taller content is clipped.
Page-number tokens (`{page}`, `{pages}`) only resolve in those two.

## Groups

A **group** repeats the bands between a `groupHeader` and the `groupFooter` with the same
`level`, once per distinct value of a data set column:

```json
{ "type": "groupHeader", "level": 1, "heightMm": 8,
  "group": { "dataSet": "sales", "field": "Region" },
  "elements": [ { "$type": "text", "content": "Region: {group.Region}" } ] },
{ "type": "table", "elements": [ { "$type": "table", "binding": { "$type": "dataSet", "name": "sales" } } ] },
{ "type": "groupFooter", "level": 1, "heightMm": 8,
  "elements": [ { "$type": "text", "content": "Subtotal: {sales.Revenue:sum:N2}" } ] }
```

- Rows group in the data set's own order — use `ORDER BY` in the query to control it.
- `{group.Field}` prints the current group's value.
- A text token over the grouped data set aggregates that group's rows, which is how a footer
  gets a subtotal. A chart bound to the same data set stays report-wide.
- A `table` band inside a group shows only that group's rows.
- Nest by opening a `level: 2` group inside a `level: 1` one.

## Page header and footer caveats

`pageHeader`/`pageFooter` bands are printed through Chromium's PDF header/footer mechanism,
which renders them as an isolated document. The CSS you pass to the exporter is inlined into
it and the bands are wrapped in the same `.pr-report` root, so your `.pr-report`-scoped rules
apply — but that document **cannot fetch anything external**, so images must be `data:` URIs
and a stylesheet referencing web fonts or `url(...)` assets will lose them.

`pageHeader`/`pageFooter` bands also print in the page *margin*, outside the report's content
box. Page `style` font, colour and alignment carry over to them, but a page border or
background frames the content only — it does not extend behind the bands.

On screen the whole thing is one continuous sheet, so these bands render once at the top and
bottom rather than repeating.

## Pagination on screen

On screen the report is one continuous sheet — it does not paginate. So a long report still
shows where the PDF will break: `ReportView` draws a faint guide line (`pr-page-break`) at
every PDF page boundary, computed from the same page size, print margin, page padding, band
heights, and footer reserve the exporter uses. It's approximate — Chromium moves a component
that doesn't fit (a chart, a table) wholly onto the next page, which shifts later breaks down.
Hide it with `.pr-page-break { display: none; }`.

The PDF has no footer by default, so its printable area matches the on-screen page exactly.
Set the page's `"showFooter": true` (designer: Page → PDF page-number strip) to print the
report name + page numbers on every page — this reserves extra bottom margin, so long content
paginates slightly earlier than the screen suggests.
