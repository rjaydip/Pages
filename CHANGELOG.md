# Changelog

Notable changes to **Pages.Reporting**, newest first.

Versions are `0.x` deliberately: until `1.0` a minor bump may carry a breaking change.
`<Version>` in `src/Pages.Reporting/Pages.Reporting.csproj` is the source of truth for the
number — see [docs/releasing.md](docs/releasing.md) for how a release is cut.

## 0.2.0 — unreleased

Nothing has been published to nuget.org yet, so this will be the first release on the gallery.

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
