# Releasing to NuGet

One package ships: **`Pages.Reporting`**, built from `src/Pages.Reporting`.
`samples/Pages.Reporting.Demo` is not packable and never publishes.

## Layout

The library is one assembly with the four former areas kept as folders:

```
src/Pages.Reporting/
  Charts/ Data/ DependencyInjection/ Model/ Rendering/ Security/ Serialization/   (the model)
  Blazor/      ReportView + element components
  Designer/    ReportDesigner + its canvas
  Export/      PDF and Excel exporters
```

**Namespaces are unchanged** from when these were four assemblies — `Pages.Reporting.Core.Model`,
`Pages.Reporting.Blazor`, `Pages.Reporting.Designer`, `Pages.Reporting.Export` all still exist,
because a namespace does not have to match an assembly name. Consumer `using` statements did
not move.

That is held together by `<RootNamespace>Pages.Reporting</RootNamespace>` plus the folder
names: a `.razor` file's namespace is derived from its folder, so `Blazor/ReportView.razor`
lands in `Pages.Reporting.Blazor` and `Designer/ReportDesigner.razor` in
`Pages.Reporting.Designer` — exactly where they were. **Renaming the `Blazor/` or `Designer/`
folder would silently move those components to a different namespace and break every consumer.**

## Metadata

All package metadata lives in `src/Pages.Reporting/Pages.Reporting.csproj`. **To release a new
version, edit `<Version>` there.**

The package icon is `assets/icon.png`, packed to the root of the `.nupkg` and named by
`<PackageIcon>`. To change it, replace that file — 128×128 PNG or JPEG, under 1 MB — and nothing
in the csproj needs touching. `assets/icon.svg` is the editable original and is not packed;
export it to `icon.png` with any SVG tool if you edit it. **The PNG is what ships**, so the two
can drift — if you change one, change the other.

Each release should also get an entry in [`CHANGELOG.md`](../CHANGELOG.md).

## Building and publishing

**The normal path is the workflow.** A release is:

1. Bump `<Version>` in the csproj and merge it to `main`.
2. **Actions → Publish to NuGet → Run workflow.**

`.github/workflows/publish.yml` is manual-only (`workflow_dispatch`) and always checks out
`main`, so the version published is whatever main's csproj says at that moment — there is no
input to override it and no push, merge or tag that can fire it by accident. The run builds with
`-warnaserror` to enforce the bar below, prints the version and commit to the run summary before
building, attaches the `.nupkg`/`.snupkg` to the run, and pushes with `--skip-duplicate` so a
re-run after a network blip is a no-op rather than a failure.

It writes nothing back to GitHub — no tags, no GitHub Releases, and nothing to GitHub Packages
(a separate registry from nuget.org). Those tabs stay empty by design; the published record is
nuget.org, and the human-readable one is [`CHANGELOG.md`](../CHANGELOG.md).

### Authentication

The workflow uses **NuGet trusted publishing** — no API key is stored anywhere. The job proves
its identity to nuget.org with a GitHub OIDC token (`NuGet/login@v1`) and receives a key valid
for roughly an hour, so there is no long-lived credential to leak or rotate. Two things must be
in place, both one-time:

- A **trusted publishing policy** on nuget.org (your account → Trusted Publishing) naming this
  repository and the workflow file, `publish.yml`. Renaming that file breaks the policy, and the
  push fails with an authentication error rather than anything that points at the rename.
- A repository **variable** `NUGET_USER` holding your nuget.org username — Settings → Secrets and
  variables → Actions → *Variables*. It is not a secret; it just keeps the username out of the
  workflow file. Inlining it in `publish.yml` instead works equally well.

`permissions: id-token: write` in the workflow is what lets the runner mint the OIDC token.
Without it the login step fails before anything is built.

Publishing by hand, should you need to:

```
dotnet build Pages.slnx -c Release
dotnet pack src/Pages.Reporting/Pages.Reporting.csproj -c Release -o ./artifacts
dotnet nuget push ./artifacts/*.nupkg --source https://api.nuget.org/v3/index.json --api-key <key>
```

Pack the project, not the solution: packing the solution also visits the demo and, because that
project sets `IsPackable=false`, NuGet warns telling you to enable packaging on it. The advice
is wrong — the demo is a sample — so packing the project directly keeps the output clean.

A `.snupkg` (symbols) is produced alongside the `.nupkg` and is pushed automatically with it.

## Before you publish

- `dotnet build Pages.slnx` reports **0 warnings, 0 errors**. Documentation generation is on,
  so an incomplete `<param>` set fails this bar — intentional, since those comments ship to
  consumers' IntelliSense.
- The version is not already on nuget.org. NuGet versions are immutable; a mistaken push can
  only be unlisted, never replaced.
- `README.md` is embedded in the package, so check it reads correctly for someone arriving from
  the nuget.org listing rather than from the repository.
- If you touched the designer's JS or scoped CSS, confirm the static web assets still resolve.
  They are keyed to the assembly name *and* the folder, so `Designer/ReportDesigner.razor.js`
  is served from `_content/Pages.Reporting/Designer/ReportDesigner.razor.js` — which is the
  path hardcoded in `ReportDesigner.razor`'s JS import. Check with:

  ```
  grep -o '_content/Pages\.Reporting[^"]*' \
    samples/Pages.Reporting.Demo/obj/Debug/net10.0/staticwebassets.build.endpoints.json | sort -u
  ```

## Package weight

The single package pulls Playwright (~195 MB, mostly bundled Node binaries for every platform)
and ClosedXML, whether or not a consumer exports anything. That is the accepted cost of shipping
one package instead of splitting export into its own; the alternative was a second package that
most users would not need.

## Platform support

The project deliberately does **not** declare `<SupportedPlatform Include="browser" />`, which
the Blazor and Designer class libraries each did while they were separate. Merged, the one
assembly also carries AES-GCM encryption, four ADO.NET drivers and Playwright — none of which
run in WebAssembly. Adding it back makes CA1416 fire on the encryption code, correctly.

## Licensing

**MIT, from 0.2.0 onward.** Two places carry it and they must agree:

- `<PackageLicenseExpression>MIT</PackageLicenseExpression>` in the csproj — an SPDX identifier,
  which nuget.org turns into a licence link on the listing. No licence file is packed.
- `LICENSE` at the repository root, for GitHub and for anyone reading the source.

A NuGet version is immutable, so a licence cannot be added to or changed on a version that is
already pushed — relicensing means publishing a new version. Note also that `dotnet pack` does
**not** warn when licence metadata is missing entirely (verified on SDK 10.0.300), so a package
with no licence builds perfectly clean; nothing in the tooling will remind you.
