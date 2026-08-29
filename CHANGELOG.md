# Changelog

Notable changes to **Pages.Reporting**, newest first.

Versions are `0.x` deliberately: until `1.0` a minor bump may carry a breaking change.
`<Version>` in `src/Pages.Reporting/Pages.Reporting.csproj` is the source of truth for the
number — see [docs/releasing.md](docs/releasing.md) for how a release is cut.

## 0.3.0 — unreleased

**In progress.** The "Data at runtime" theme from [ROADMAP.md](ROADMAP.md): supplying a
report's inputs at generation time rather than baking them into the JSON. Expected to be
additive — no model removals — but `0.x` minor bumps may still carry a breaking change.

Planned scope (tick as it lands):

- [x] **Connection strings supplied at runtime** — pass a connection at generation time, the
  way parameters already are, so one definition runs against dev / staging / prod or a
  per-tenant database.
- [x] **Typed parameters** — a type on `ParameterDefinition` (number, date, boolean, list of
  allowed values) driving real inputs in `<ReportParameters>` and letting the designer
  validate a default before a query runs.
- [x] **One runtime-input concept.** A parameter can be a reader fill-in field or
  host-supplied (`ParameterDefinition.AcceptsUserInput`); `{@name}` in text and `@name` in SQL
  are the one syntax. The `{= … }` expression language and table row numbering / running-total
  columns are tracked for 0.4.0.

### Added

- **Connection strings supplied at runtime.** A connection can be marked
  `suppliedAtRuntime` (`ConnectionDefinition.SuppliedAtRuntime`, a checkbox in the designer):
  its string is not stored, and the caller passes one at generation time. One definition then
  runs against dev/staging/prod or a per-tenant database without duplicating the JSON. Keyed by
  connection name (case-insensitive); the provider stays in the report; a missing value renders
  a *"must be supplied at generation time"* banner. Never persisted, never exposed on
  `ResolvedReport`.
- **`ReportRuntimeOptions`** — the single object carrying everything supplied at generation
  time (`Parameters`, `ConnectionStrings`). Passed to `IReportGenerator.GenerateAsync`,
  `PdfReportExporter.ExportAsync` and `ExcelReportExporter.ExportAsync`. `<ReportView>` gains
  a matching `ConnectionStrings` parameter alongside `Parameters`.
- **`ParameterDefinition.AcceptsUserInput`** (JSON `acceptsUserInput`, default `true`). A
  parameter marked `false` is host-supplied only — the signed-in user, the tenant, a
  correlation id: it never appears in `<ReportParameters>`, but is still usable as `@name` in
  SQL and `{@name}` in text. `<ReportParameters>` renders inputs only for parameters that
  accept user input; the host merges those with its own values (host last) before passing
  them on. Old report JSON without the field reads as `true` — every existing parameter is a
  fill-in field, unchanged.
- **Typed parameters — `ParameterDefinition.Type`** (`text` default / `number` / `date` /
  `boolean` / `list` with `allowedValues` as `{value, label}` pairs). `<ReportParameters>`
  renders a matching control (number spinner, date picker, checkbox, choice dropdown). A
  `number` binds as `decimal` (currency comparisons are exact); a `boolean` binds as `1` / `0`
  (the form `bit` / `INTEGER` columns expect); a `date` binds a canonical ISO string. A typed
  value the reader
  picks is canonicalised — `{@name}` and the query see the same form. A value that doesn't
  match its type (only reachable from a host dictionary or a bad default) is filtered as SQL
  `NULL` and shows a report-level banner. The designer flags a default that doesn't parse for
  its type. Parsing is InvariantCulture / ISO-8601 — a report-level culture is 0.4.0. Untyped
  parameters (`type` absent) behave exactly as before.

- **`GenerateAsync` / `ExportAsync` / `ResolveAsync` now take `ReportRuntimeOptions? options`
  in place of `IReadOnlyDictionary<string,string?>? parameters`.** Migration: wrap the map,
  `ExportAsync(json, parameters: v)` → `ExportAsync(json, new ReportRuntimeOptions { Parameters = v })`.
  **Recompile required**; a custom `IReportGenerator` implementation must update its signature.
- **Connection-failure banners no longer include the provider's message** (it can carry the
  connection string). They now read *"Could not connect to '{name}'."*; the full exception is
  logged server-side at Warning. `Pages.Reporting` now depends on
  `Microsoft.Extensions.Logging.Abstractions`.
- A blank connection string serialises as `""` rather than an encrypted empty token.
- **`{@name}` values are HTML-escaped in a `renderHtml` text element** (they are data, not
  markup — a value carrying `<`/`&` could previously inject tags). The author's own literal
  markup is unaffected, and a value with no markup characters is byte-identical.
- **Text-token substitution is one shared renderer** — the resolve-time and per-group
  re-render paths were near-duplicate loops that could drift; they now share `TextRenderer`.
  Token output is unchanged. Two second-order effects: `{now}` is captured once per
  generation (two `{now}` in a report can no longer land a second apart), and when more than
  one connection fails the order they appear in the report-level banner can differ, because
  a data set named only by a text token is now queried in an up-front pass.
- A map key with a leading `@` (`{ ["@region"] = … }`) now hits the same parameter as
  `"region"`.
- A `type: number` parameter binds to SQL as `decimal` (was `double` for a value with a
  decimal point) — exact against `DECIMAL`/`MONEY` columns.
- `{@name}` for a typed parameter emits the canonical form (`2026-1-9` → `2026-01-09`). No
  existing report is affected — `type` defaults to `text`.

### Fixed

- **Runtime parameter names are now matched case-insensitively** — `?MinRevenue=5000` reached a
  report declaring `@minRevenue` as an unmatched key and silently did nothing.

## 0.2.1 — 2026-08-09

Documentation only. No library code changed, so upgrading from 0.2.0 is optional — but the
package page on nuget.org shows the README embedded in each version, and 0.2.0's is the older
one. This release exists to correct it.

### Changed

- **README links now work from nuget.org.** Seven links were relative (`docs/…`, `LICENSE`,
  `CHANGELOG.md`), which resolve on GitHub and 404 for anyone arriving from the package page.
  They are absolute now.
- **README trimmed** from 226 lines to roughly 170. Nothing was dropped: the Blazor support
  matrix and the Chromium pre-install guide moved into the new
  [hosting guide](https://github.com/rjaydip/Pages/blob/main/docs/hosting.md).
- **Removed the downloads badge.** NuGet's download statistics lag their own search index by
  hours, so the badge and nuget.org disagreed with each other; the number means little for a
  package this new either way.

### Added

- **Screenshots in the README** — the designer, the same definition printed to PDF, and
  parameters applied in the preview.
- **[Hosting guide](https://github.com/rjaydip/Pages/blob/main/docs/hosting.md)** — supported
  Blazor render modes, pre-installing Chromium without PowerShell, and why fonts must be
  installed where Chromium runs.
- **[ROADMAP.md](https://github.com/rjaydip/Pages/blob/main/ROADMAP.md)** — what is planned, and
  which of it already exists so it does not get built twice.

## 0.2.0 — 2026-08-07

First release on nuget.org.

### Added

- **Font family for text elements.** `ElementStyle.FontFamily` (JSON: `fontFamily`) takes a CSS
  font stack. The designer offers 30 preset stacks grouped by typeface, plus a custom box for
  your own. It applies on screen, in the PDF, on the designer canvas, and in the Excel export —
  which reduces the stack to the single family name Excel accepts, since a cell has no fallbacks.
- **`<ReportParameters>` component.** Unstyled like the rest of the `pr-*` components: it renders
  one input per declared report parameter, seeded from the defaults, and raises the values on
  Apply. Renders nothing when a report declares no parameters.
- **Parameter values in the designer's Preview**, using that component, so a report can be tried
  against real values without leaving the designer.
- **Package icon** (`assets/icon.png`) and an **MIT licence**, both carried in the package.
- **Manual publish workflow** (`.github/workflows/publish.yml`) — publishes `main` to nuget.org
  on demand from the Actions tab.

### Changed

- **The designer fills the full width of its container.** It previously capped its three-column
  layout at roughly 1424px and centred it, leaving dead space on either side on a wide screen.
- **Preview shows parameters in a panel beside the sheet** rather than a bar above it, which was
  spending vertical space in a view that is already height-constrained.
- **The preview sheet no longer inherits the designer's own chrome font**, so an unstyled report
  previews the way it actually renders rather than looking better than it is.

### Fixed

- **A font stack containing double quotes no longer breaks the PDF's page header and footer.**
  Style declarations are now HTML-encoded into that attribute; `"Segoe UI", sans-serif` is
  ordinary CSS, so this fired on correct input, not just malicious input.
- **Excel no longer applies a font to the blank spacer row** after a table block, which had
  extended the workbook's formatted range by a row.
- **The properties panel no longer shows an empty "Position" heading** for the element in a table
  band, which has no draggable rectangle and so nothing to put under that heading.
- **Demo: saving an existing report no longer reverts the designer canvas** to the pre-save JSON.

### Documentation

- README now states the Blazor hosting requirement explicitly — server-side only, with a support
  matrix — and documents pre-installing Chromium without PowerShell.
- `docs/releasing.md` covers the icon, the licence, and the publish workflow.

## 0.1.0

Not recorded. This repository has a single initial commit and no tags, and nothing was published
to nuget.org under this version, so its contents cannot be reconstructed from the repository.
