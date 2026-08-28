# The report definition

A report is one self-contained JSON document. This page covers what goes in it: data sets,
text expressions, table columns, and parameters.

- [Bands and groups](bands-and-groups.md) — the page structure
- [Styling](styling.md) — CSS hooks and the `style` block

## Data sets — one query, many components

Data comes from **named, report-level data sets**: a connection + a query (or inline static
rows), declared once and referenced by any number of components. Each data set executes
**once per generation**, no matter how many components read it.

```json
"dataSets": [
  { "name": "sales",
    "binding": { "$type": "sql", "connection": "demo",
                 "sql": "SELECT Region, Product, Quantity, Revenue FROM Sales" } }
],
"bands": [
  { "type": "content", "heightMm": 40, "elements": [
    { "$type": "text", "content": "Total revenue\n{sales.Revenue:sum:C0}",
      "position": { "x": 0, "y": 0, "width": 56, "height": 40 } } ] },
  { "type": "table", "elements": [
    { "$type": "table", "binding": { "$type": "dataSet", "name": "sales" } } ] }
]
```

- **Rows consumers** (table, chart) read the data set's rows directly.
- **Text** has no binding at all — it reads its column inside the token itself,
  `{dataSet.Column:aggregate:format}`. `field` and `aggregate` on a `DataSetBinding` are only
  ever populated on a throwaway binding the resolver builds from that token; set them on a
  stored binding and nothing reads them.
- Per-element embedded queries (`"$type": "sql"` / `"inline"` bindings) from older JSON remain
  fully supported at render time; the designer offers a one-click switch to a data set when you
  select such a component.

## Text expressions

The **Text** component mixes literal text with data placeholders resolved from the report's
data sets and parameters:

```json
{ "$type": "text", "content": "Total revenue: {sales.Revenue:sum:C0} across {sales.Region:count} sales" }
```

- `{dataSet.Column}` — first row's value; `{dataSet.Column:sum}` — aggregate
  (`first`/`sum`/`avg`/`count`/`min`/`max`); `{dataSet.Column:sum:C0}` — plus a .NET format
  string. Format-only also works (`{sales.Revenue:C0}`) since aggregates are known keywords.
- `{@paramName}` inserts a report parameter's value; `{{` writes a literal `{`.
- Line breaks in `content` are preserved. A failed expression renders inline (⚠) without
  breaking the rest of the text.
- `"renderHtml": true` renders the resolved content as raw HTML, so author-written tags
  (`<b>`, `<span style="…">`, …) take effect — on screen and in the PDF. Only enable it for
  content you trust: the text is emitted unescaped.

### Built-in tokens

Any text element can use these alongside the usual expressions:

| Token | Value |
|---|---|
| `{page}` / `{pages}` | current page number / total page count |
| `{reportName}` | the report's name |
| `{now}`, `{now:d}`, `{now:yyyy-MM-dd}` | generation time, optional .NET format |

`{page}` and `{pages}` only produce real numbers in a `pageHeader`/`pageFooter` band, because
only the printer knows what page it is on. Anywhere else — any other band type, the on-screen
view, Excel — they read as `1`.

## Table columns

Leave a table's `columns` empty and it shows every column the query returned, unformatted.
Declare them to control what appears and how:

```json
{ "$type": "table", "binding": { "$type": "dataSet", "name": "sales" },
  "columns": [
    { "field": "Product" },
    { "field": "Quantity", "align": "right" },
    { "field": "Revenue", "header": "Revenue (USD)", "format": "N2", "aggregate": "sum" }
  ] }
```

| Field | Effect |
|---|---|
| `field` | column name in the result set (required) |
| `header` | header text; defaults to the field name |
| `format` | .NET format string — `N2`, `C0`, `#,0`, `P1`, `dd-MMM-yyyy` |
| `align` | `auto` (default), `left`, `center`, `right` |
| `aggregate` | `none`, `sum`, `average`, `count`, `min`, `max` — adds a `<tfoot>` total row |

`align: auto` right-aligns a column whose values are numeric and left-aligns everything else,
probed across the whole data set so a column stays put between group repeats. `aggregate`
totals only numeric values, ignores nulls, and formats the result with that column's own
`format` (falling back to `#,0.##`); inside a group it totals that group's rows, which is how
a group footer gets a subtotal.

### `format` applies to numbers and dates, not strings

**`format` only applies to values implementing `IFormattable`** — numbers, `DateTime`,
`decimal`. **Strings pass through untouched.**

So a date stored as SQLite `TEXT` arrives as a string and `"format": "dd-MMM-yyyy"` silently
does nothing. Declaring the column `DATETIME` does not help, because SQLite returns the
storage class. Format it in the query instead:

```sql
SELECT strftime('%d-%m-%Y', SaleDate) AS SaleDate FROM Sales
```

SQL Server and PostgreSQL return a real `DateTime` from a date column, so the format applies
as written.

An invalid format string never breaks a report: the value renders unformatted rather than
throwing, since a throw mid-render would take the whole PDF with it.

## Parameters

Reports can declare named parameters with defaults; data set SQL references them as `@name`,
and the host overrides them at generation time — a natural fit for URL query values:

```json
"parameters": [ { "name": "minRevenue", "defaultValue": "0" } ],
"dataSets":   [ { "name": "topSales", "binding": { "$type": "sql", "connection": "demo",
                  "sql": "SELECT * FROM Sales WHERE Revenue >= @minRevenue" } } ]
```

```csharp
var values = new Dictionary<string, string?> { ["minRevenue"] = "5000" }; // e.g. from the URL
<ReportView Json="@json" Parameters="values" />
byte[] pdf = await pdfExporter.ExportAsync(json, new ReportRuntimeOptions { Parameters = values });
```

`GenerateAsync` and the `ExportAsync` overloads take a single `ReportRuntimeOptions` — it
carries `Parameters` and (below) `ConnectionStrings`. `<ReportView>` keeps the discrete
`Parameters` / `ConnectionStrings` parameters and builds the options object for you.

For a UI rather than a hard-coded dictionary, drop in the unstyled `<ReportParameters>`
component. It renders one input per declared parameter, seeded from the defaults, and raises the
values when the reader presses Apply:

```razor
<ReportParameters Report="report" Values="_values" ValuesChanged="v => _values = v" />
<ReportView Report="report" Parameters="_values" />
```

It renders nothing at all when a report declares no parameters, so it is safe to place
unconditionally. An empty box means "use that parameter's default" — the name is left out of the
dictionary rather than sent as an empty string.

The page (or this component) needs an interactive render mode, e.g. `@rendermode InteractiveServer` —
it applies values through `@onchange`/`@onsubmit` handlers rather than a form post, and those do
nothing under static SSR.

Only declared parameters are applied (unknown names are ignored), names are matched
case-insensitively, and a parameter is bound to a query only when its `@name` appears in that
SQL.

### Numbers vs. zero-padded codes

**Values travel as strings, and are bound as numbers only when the text is exactly how that
number writes itself.** `"5000"` binds as a number, so `Revenue >= @minRevenue` compares
numerically. `"02"` does *not* — a zero-padded code is not the number 2, and binding it as one
would stop it matching the text column it came from. The same rule keeps `" 2"`, `"+2"` and
`"1,000"` as text, while `"1.50"` binds as a number and keeps its scale.

This matters most for zero-padded keys. `strftime('%m', SaleDate)` yields `'02'`, so the
parameter must be `02` — passing `2` correctly matches nothing:

```json
"parameters": [ { "name": "month", "defaultValue": "02" } ],
"dataSets":   [ { "name": "monthly", "binding": { "$type": "sql", "connection": "demo",
                  "sql": "SELECT * FROM Sales WHERE strftime('%m', SaleDate) = @month" } } ]
```

Note the placeholder is bare. Quoting it — `= '@month'` — makes it a SQL string literal
containing the eight characters `@month`, which matches nothing and raises no error.

## Connection-string encryption

AES-256-GCM. The key is resolved in order:

1. `AddPagesReporting(o => o.EncryptionKey = ...)` — base64 of 32 bytes, or any passphrase
2. `PAGES_REPORTING_KEY` environment variable
3. an auto-generated `pages-reporting.key` file next to the app (created on first use)

Reports encrypted with one key can only be generated where that key is available. Supported
databases — **SQL Server, SQLite, PostgreSQL, MySQL** — ship with the library; consuming apps
install no ADO.NET packages.

## Connections at runtime

A connection can be left out of the report and supplied at generation time instead — the way
parameters are — so one definition runs against dev, staging and production, or against a
per-tenant database, without duplicating the JSON.

Mark the connection `suppliedAtRuntime` (the designer has a checkbox, *"Supplied at generation
time"*). Its `connectionString` is then stored empty; the **provider stays in the report**:

```json
"connections": [
  { "name": "sales", "provider": "PostgreSql", "suppliedAtRuntime": true, "connectionString": "" }
]
```

Pass the string at generation time, keyed by connection name:

```csharp
var conns = new Dictionary<string, string?> { ["sales"] = "Host=db.internal;Database=acme;Username=…;Password=…" };
<ReportView Json="@json" ConnectionStrings="conns" />
byte[] pdf = await pdfExporter.ExportAsync(json, new ReportRuntimeOptions { ConnectionStrings = conns });
```

- Keys are connection names, matched **case-insensitively**. Only connections marked
  `suppliedAtRuntime` read from the map; any other key is ignored, and a runtime value can
  never redirect a connection whose string is stored.
- The value is used **as-is** — the provider comes from the report's connection definition,
  not the string.
- If a `suppliedAtRuntime` connection gets no value (or an empty one), the report renders with
  a banner: *"Connection 'sales' must be supplied at generation time."*
- The supplied string lives in memory for that one generation only. It is never written to the
  report JSON and never exposed on `ResolvedReport`. Read it from configuration or a secret
  store — **never from a query string or other client input.**
- `<ReportView Resolved="…">` is already resolved, so it ignores `ConnectionStrings`; the
  designer's own preview and column discovery always use the authored connection.

One report definition run across many tenants opens one ADO.NET connection pool per distinct
connection string. At high tenant counts, tune pooling (`Max Pool Size`, `Pooling=false`) or
reuse strings deliberately.
