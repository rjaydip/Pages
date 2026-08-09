# Hosting and deployment

What the library needs from the app that hosts it, and how to get Chromium in place before the
first PDF export rather than during it.

## Blazor hosting

Pages.Reporting carries ADO.NET drivers, AES-GCM encryption and Playwright, none of which run in
the browser sandbox, so it needs a render mode that executes on the server:

| Host | Supported |
|---|---|
| Blazor Server | yes |
| Blazor Web App — `InteractiveServer` render mode | yes |
| Blazor Web App — static SSR (no render mode) | yes, for `<ReportView>` and the export APIs |
| Blazor Web App — `InteractiveWebAssembly` or `InteractiveAuto` | no |
| Blazor WebAssembly standalone | no |

`<ReportDesigner>` is drag-and-drop and uses JS interop, so the page hosting it needs
`@rendermode InteractiveServer`. `<ReportView>` has no such requirement and renders fine under
static SSR — that is also how the PDF exporter renders it.

If your app is a WebAssembly or Auto Blazor Web App, keep the reporting pages in the server
project with `@rendermode InteractiveServer`; the rest of the app is unaffected.

`<ReportParameters>` applies its values through event handlers rather than a form post, so it
also needs an interactive render mode.

## Chromium, for PDF export

PDF export prints through Playwright's Chromium — a ~150 MB browser download, once. Nothing extra
is needed for Excel export or on-screen rendering.

Because everything ships as one package, the Playwright dependency (~195 MB, mostly bundled Node
binaries) comes along even if you never export a PDF. The Chromium *browser* is a separate
download that only happens if you actually run one.

**It downloads automatically on the first PDF export** — `PdfReportExporter` catches the "browser
not installed" failure, downloads it once, then retries. If that suits you, there is nothing to
do.

### Pre-installing it

Recommended for servers, containers and CI, where the first request should not pay for a 150 MB
download. Call the same installer the exporter calls — a plain .NET entry point, so **no
PowerShell is involved**. Add a switch to your `Program.cs`:

```csharp
// before builder.Build(), so a deploy step can pay the download instead of the first request
if (args.Contains("--install-browsers"))
{
    return Microsoft.Playwright.Program.Main(["install", "chromium"]);
}
```

and run it once after building:

```
dotnet run -- --install-browsers
```

On **Linux**, pass `["install", "--with-deps", "chromium"]` instead. `--with-deps` also installs
the system libraries headless Chromium needs (fonts, `libnss3`, `libgbm1`, and friends); without
them Chromium is present but fails to launch. It installs system packages, so it needs root —
which is what you already have in a Dockerfile:

```dockerfile
RUN dotnet MyApp.dll --install-browsers
```

Playwright's `playwright.ps1` script still appears next to your binary and does the same job if
you would rather use it, but nothing here requires it.

To keep the browser somewhere other than the default per-user cache — a shared image layer, say —
set `PLAYWRIGHT_BROWSERS_PATH` to the same directory when installing and when running.

## Fonts

A font only appears in the PDF if it is installed on the machine where Chromium runs, or
delivered by your own CSS via `@font-face`. Containers typically have very few fonts installed,
which is why every font the designer offers ends in a generic family — see
[styling](styling.md).
