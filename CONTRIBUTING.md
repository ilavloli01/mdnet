# Contributing to mdnet

This guide covers building, testing and releasing mdnet. For how to *use* the tool, see [README.md](README.md).

## Prerequisites

- .NET 10 SDK. The exact version is pinned in `global.json`, with `latestFeature` roll-forward.
- [bun](https://bun.sh), to build the HTML renderer. `node` can run the built renderer, but the build scripts use bun.
- `git`, for the git-source code paths.

## Repository layout

```
src/Mdnet/                 the CLI tool (packed as the `mdnet` dotnet tool)
  Cli/                     System.CommandLine commands: generate, render, download, sync
  Loading/                 project discovery, MSBuild properties, restored packages (project.assets.json)
  Extraction/              Roslyn symbols → documentation model
  Docs/                    XML doc comment parsing (incl. inheritdoc)
  Signatures/              C# signature rendering and visibility filtering
  Model/                   documentation model
  Markdown/                Markdown pages, namespace tree, README import
  Publishing/              mdnet.json manifests, root index, download/sync sources
  Rendering/               runs the bundled Markdoc renderer with bun or node
renderer/                  TypeScript Markdoc + React + Shiki renderer, bundled to renderer/dist/render.mjs
samples/Sample.Lib/        sample library used as test input (source mode and, packed, package mode)
tests/Mdnet.Tests/         TUnit tests and verified snapshots
```

## Build the renderer

The .NET tool ships `renderer/dist/render.mjs`. Build it before running HTML output or packing:

```sh
cd renderer
bun install
bun run typecheck
bun run build        # → renderer/dist/render.mjs
cd ..
```

`dotnet pack` fails with a clear error if the bundle is missing. To try a renderer without rebuilding the tool, point `MDNET_RENDERER` at a `render.mjs` file.

## Run from source

```sh
dotnet run --project src/Mdnet -- generate samples/Sample.Lib -o out
dotnet run --project src/Mdnet -- generate samples/Sample.Lib -f md -o out -v
dotnet run --project src/Mdnet -- render out/md -o out/site
```

`out/`, `nupkg/` and `.mdnet/` are git-ignored.

## Test

```sh
dotnet test --project tests/Mdnet.Tests
```

Snapshot tests compare the generated output with `tests/Mdnet.Tests/Snapshots/*.verified.*`. On a mismatch, a `*.received.*` file is written next to the snapshot (git-ignored). Review the diff, then accept the change:

```sh
MDNET_ACCEPT=1 dotnet test --project tests/Mdnet.Tests          # bash
$env:MDNET_ACCEPT=1; dotnet test --project tests/Mdnet.Tests    # PowerShell
```

The package-mode test packs `samples/Sample.Lib` into a temporary feed. Keep that project packable, and keep its own package metadata.

## Pack and try the package locally

```sh
dotnet pack src/Mdnet -c Release                  # → nupkg/mdnet.<version>.nupkg
```

Run the packed tool without installing it, using `dnx` (the .NET 10 equivalent of `npx`):

```sh
dnx mdnet --yes --add-source ./nupkg -- generate samples/Sample.Lib -o out
```

`dnx` caches the package by version. After repacking the same version, clear the cached copy first (`~/.nuget/packages/mdnet/<version>`), or pack with a new version: `-p:Version=0.1.1-dev.1`, then run it with `dnx mdnet@0.1.1-dev.1 --yes --prerelease --add-source ./nupkg -- …`.

Or install the packed tool into a folder:

```sh
dotnet tool install mdnet --tool-path ./.tool --add-source ./nupkg --prerelease
./.tool/mdnet generate samples/Sample.Lib -o out
```

Check that the package contains `README.md` and `LICENSE`, and that the `.nuspec` metadata is correct: open the `.nupkg` as a zip file.

## Package metadata

- Shared properties (`Authors`, `RepositoryUrl`, `PackageReadmeFile`) live in `Directory.Build.props`.
- Tool-specific properties (`PackageId`, `Version`, `Description`, `PackageTags`, `PackageLicenseFile`, `PackageProjectUrl`) live in `src/Mdnet/Mdnet.csproj`.
- The root `README.md` is the nuget.org package page. Use absolute URLs for links to other repository files, because relative links do not work on nuget.org.

## CI and releases

`.github/workflows/ci.yml` runs on every push and pull request, on Ubuntu and Windows:

1. Builds and typechecks the renderer.
2. Runs the tests.
3. Packs `0.1.0-ci.<run>` and smoke-tests the installed tool (Ubuntu only).

To release, push a tag `v<version>` (for example `v0.2.0`). The package is packed with that version and pushed to nuget.org, using the `NUGET_API_KEY` repository secret.

## Guidelines

- `TreatWarningsAsErrors` is on. Keep builds warning-free.
- Add or update tests for behavior changes. Accept snapshot changes only after reviewing the diff.
- If a change affects CLI options, output layout or `mdnet.sources.json`, update `README.md` too, because it is the user and agent guide.
