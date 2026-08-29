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
- `{@name}` inserts a report parameter's value — declared reader fill-in values and
  host-supplied ones alike (see [Parameters](#parameters)). `{{` writes a literal `{`.
- Line breaks in `content` are preserved. A failed expression renders inline (⚠) without
  breaking the rest of the text.
- `"renderHtml": true` renders the resolved content as raw HTML, so author-written tags
  (`<b>`, `<span style="…">`, …) take effect — on screen and in the PDF. Only enable it for
  content you trust: the author's own markup is emitted unescaped. `{@name}` and
  `{reportName}` **values** are HTML-escaped in this mode so a host- or reader-supplied value
  cannot inject tags; `{dataSet.Column}` values are not.

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

`{now}` is captured once per generation, so every `{now}` in a report — including one that a
group band re-renders per group — shows the same instant. It is the server's local time.

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

A report declares its named inputs once, in `parameters`. Data set SQL references one as
`@name`, text as `{@name}`. The host supplies values at generation time, falling back to
`defaultValue` when none is given.

```json
"parameters": [
  { "name": "minRevenue", "defaultValue": "0" },
  { "name": "user", "acceptsUserInput": false }
],
"dataSets": [ { "name": "topSales", "binding": { "$type": "sql", "connection": "demo",
                "sql": "SELECT * FROM Sales WHERE Revenue >= @minRevenue" } } ]
```

```csharp
var values = new Dictionary<string, string?>
{
    ["minRevenue"] = readerInput,       // what the reader typed
    ["user"] = User.Identity!.Name,     // host-supplied — set last, so a reader cannot spoof it
};
<ReportView Json="@json" Parameters="values" />
byte[] pdf = await pdfExporter.ExportAsync(json, new ReportRuntimeOptions { Parameters = values });
```

`GenerateAsync` and the `ExportAsync` overloads take a single `ReportRuntimeOptions` — it
carries `Parameters` and `ConnectionStrings` (below). `<ReportView>` keeps the discrete
`Parameters` / `ConnectionStrings` component parameters and builds the options object for you.

### Reader fill-in vs. host-supplied

`acceptsUserInput` (default `true`) decides whether a parameter is a field the report reader
sets or a value the host injects:

| | `acceptsUserInput: true` | `acceptsUserInput: false` |
|---|---|---|
| Shown in `<ReportParameters>` | yes | no |
| Set by | the reader (a form field, a URL value) | the host app only (`{@user}`, `{@tenant}`) |
| Usable as `@name` in SQL and `{@name}` in text | yes | yes |

The distinction is UI only — the resolver applies whatever value it is given for a declared
name, regardless of the flag.

### The `<ReportParameters>` form

For a UI rather than a hard-coded dictionary, drop in the unstyled `<ReportParameters>`
component. It renders one input per **reader fill-in** parameter, seeded from the defaults,
and raises those values when the reader presses Apply. Your page merges the raised values with
the host-supplied ones — host last, so a reader cannot override a host value — before handing
the result to `<ReportView>`:

```razor
<ReportParameters Report="report" Values="_readerValues" ValuesChanged="v => _readerValues = v" />
<ReportView Report="report" Parameters="Merged()" />

@code {
    IReadOnlyDictionary<string, string?> _readerValues = new Dictionary<string, string?>();

    IReadOnlyDictionary<string, string?> Merged()
    {
        var m = new Dictionary<string, string?>(_readerValues, StringComparer.OrdinalIgnoreCase);
        m["user"] = CurrentUser;          // host-supplied wins
        return m;
    }
}
```

It renders nothing when a report has no reader fill-in parameters, so it is safe to place
unconditionally. An empty box means "use that parameter's default" — the name is left out of
the dictionary rather than sent as an empty string. The page (or this component) needs an
interactive render mode, e.g. `@rendermode InteractiveServer`.

### Rules

- Only **declared** names are applied — an unknown key in the map is ignored, and `{@typo}`
  in text renders `⚠ unknown parameter 'typo'`.
- Names are matched **case-insensitively**; a leading `@` on a map key is ignored.
- A value reaches a data set's query only when that query's SQL text contains `@name`.
- **Take values from server configuration or your request context, not straight from
  client-supplied input.** A value bound into a query is always a safe `DbParameter` (no SQL
  injection), but the runtime map has no allow-list of its own — if you project a whole query
  string into it, `?tenantId=999` from a reader's URL can change what a query returns. Filter
  untrusted input to the names you intend readers to control, and set host values last.

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
