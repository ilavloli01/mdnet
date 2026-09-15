# mdnet

AI-friendly API documentation for .NET libraries.

`mdnet` is a .NET CLI tool that:

1. **Generates** signature-first Markdown API docs from C# source code or from referenced NuGet packages, using Roslyn. Agents can read these docs directly.
2. **Renders** that Markdown into a static HTML site with [Markdoc](https://markdoc.dev), React and [Shiki](https://shiki.style).
3. **Downloads and syncs** docs that others have published (over HTTP, from git, or from a local folder), so an agent can read docs for your dependencies without generating them.

**Contents**

- [Requirements](#requirements)
- [Install or run](#install-or-run)
- [Quick start](#quick-start)
- [Commands](#commands): [`generate`](#mdnet-generate), [`render`](#mdnet-render), [`download`](#mdnet-download), [`sync`](#mdnet-sync)
- [Output layout](#output-layout)
- [Writing docs that mdnet picks up](#writing-docs-that-mdnet-picks-up)
- [Publishing docs](#publishing-docs)
- [Consuming dependency docs (`mdnet.sources.json`)](#consuming-dependency-docs-mdnetsourcesjson)
- [Using mdnet with AI agents](#using-mdnet-with-ai-agents)
- [Troubleshooting](#troubleshooting)

## Requirements

| Requirement | Needed for |
|---|---|
| .NET 10 SDK or newer | Always. The tool runs on .NET 10+, and `generate` loads projects through MSBuild. |
| `bun` or `node` on `PATH` | HTML output only (`generate` with `html` format, and `render`). Nothing is installed from npm: the renderer ships inside the tool as one bundled file. |
| `git` on `PATH` | Git sources in `download` and `sync` only. |

## Install or run

Choose one of the following options.

### Option A: run without installing (`dnx`)

.NET 10 includes `dnx`, which works like `npx`: it downloads the tool package into the NuGet cache and runs it. This is the recommended way for CI and for AI agents, because nothing has to be installed first.

```sh
dnx mdnet --yes -- generate MySolution.slnx -o docs
```

- `--yes` accepts the download prompt, so the command runs non-interactively.
- Pin a version with `mdnet@<version>`, for example `dnx mdnet@0.1.0 --yes -- generate .`
- Put `--` before the mdnet command. Everything after `--` goes to mdnet. Without it, `dnx` handles options such as `--help`, `-v` and `--version` itself.
- `dnx` is shorthand for `dotnet tool exec`.

### Option B: global tool

```sh
dotnet tool install -g mdnet
mdnet --help
```

Update with `dotnet tool update -g mdnet`. Uninstall with `dotnet tool uninstall -g mdnet`.

### Option C: local tool (version pinned in the repository)

```sh
dotnet new tool-manifest        # once per repository; creates .config/dotnet-tools.json
dotnet tool install mdnet
dotnet tool run mdnet -- --help # or: dotnet mdnet --help
```

After a fresh clone, run `dotnet tool restore`.

> In the rest of this guide, `mdnet <command>` works the same as `dnx mdnet --yes -- <command>` and `dotnet mdnet <command>`.

## Quick start

Document your own library and give an agent the entry point:

```sh
# 1. Generate Markdown + HTML for every project in the solution (test projects are skipped)
mdnet generate MySolution.slnx -o docs

# 2. Agents read this file first
#    docs/md/index.md

# 3. Open the HTML site
#    docs/html/index.html
```

Get docs for your dependencies instead:

```sh
# 1. Create mdnet.sources.json next to the solution (see "Consuming dependency docs")
# 2. Download the docs for every matching package reference
mdnet sync MySolution.slnx

# 3. Agents read this file first
#    .mdnet/docs/index.md
```

## Commands

Every command accepts these options:

| Option | Description |
|---|---|
| `--verbose`, `-v` | Show detailed progress. |
| `--quiet`, `-q` | Only show warnings and errors. |
| `--help`, `-h`, `-?` | Show help for the command. |

Exit code `0` means success. Exit code `1` means an error; the error message is printed.

Options that take lists (`--packages`, `--format`, `--include`, `--exclude`, `--path`) accept several values after one flag (`-f md html`), the flag repeated (`-p "A.*" -p "B.*"`), or `;`/`,` separated patterns (`-p "A.*;B.*"`). Name patterns are case-insensitive and support `*` and `?`.

### `mdnet generate`

Generates docs from source code or from referenced NuGet packages.

```
mdnet generate [<path>] [options]
```

| Argument / option | Default | Description |
|---|---|---|
| `<path>` | `.` | Solution (`.sln`/`.slnx`), project (`.csproj`) or directory. |
| `--packages`, `-p` | – | Switches to **package mode**: documents the NuGet packages referenced by the projects whose id matches the pattern (for example `"Acme.*"`). Without this option, mdnet runs in **source mode**. |
| `--output`, `-o` | `docs` | Output directory. Markdown goes to `<output>/md`, HTML to `<output>/html`. |
| `--format`, `-f` | `md html` | Output formats: `md`, `html`. |
| `--visibility` | `protected` | Lowest accessibility to document: `public`, `protected` or `internal`. |
| `--include` | – | Source mode: only document projects whose name matches. |
| `--exclude` | `*.Benchmarks *.Samples` | Source mode: skip projects whose name matches. Test projects are always skipped. |
| `--runtime` | `auto` | JavaScript runtime used to render HTML: `auto`, `bun` or `node`. |

Examples:

```sh
# Source mode: a solution, a project, or a directory
mdnet generate MySolution.slnx -o docs
mdnet generate src/MyLib/MyLib.csproj -o docs
mdnet generate src/MyLib -f md                       # Markdown only, no JavaScript runtime needed

# Only some projects, public API only
mdnet generate MySolution.slnx --include "MyCompany.*" --exclude "*.Internal" --visibility public

# Package mode: document referenced NuGet packages (run `dotnet restore` first)
mdnet generate MySolution.slnx --packages "Acme.*" -o docs/deps
```

Package mode reads the restored references of every project, including transitive ones, and loads the assemblies and XML doc files from the NuGet packages folder.

### `mdnet render`

Renders a Markdown docs folder, either generated or downloaded, into a static HTML site. The output folder is replaced on every render.

```
mdnet render <input> --output <dir> [options]
```

| Argument / option | Default | Description |
|---|---|---|
| `<input>` | *(required)* | Markdown docs root, for example `docs/md` or `.mdnet/docs`. |
| `--output`, `-o` | *(required)* | HTML output directory. |
| `--runtime` | `auto` | `auto`, `bun` or `node`. |

```sh
mdnet render .mdnet/docs -o .mdnet/site
```

### `mdnet download`

Downloads published docs into a local docs root, once. Only changed files are fetched, and every file is checked against the hash in its `mdnet.json`.

```
mdnet download <source> [options]
```

| Argument / option | Default | Description |
|---|---|---|
| `<source>` | *(required)* | HTTP(S) URL of a docs folder (one that contains `mdnet.json`), a git repository URL, or a local directory. |
| `--ref` | – | Git branch, tag or commit. |
| `--path` | every docs folder | Folders inside the git repository that hold package docs. |
| `--git` | `false` | Treat the source as a git repository even if the URL does not look like one. |
| `--output`, `-o` | `.mdnet/docs` | Docs root. Each package goes to `<output>/<id>`. |

A source is treated as git if it ends with `.git`, starts with `git@`, `ssh://` or `git://`, is a `github.com`/`gitlab.com`/`bitbucket.org` repository root URL, or if `--git`, `--ref` or `--path` is given.

```sh
mdnet download https://docs.example.com/Acme.Core/
mdnet download https://github.com/org/acme-docs --ref v1.2.0 --path docs/Acme.Core
mdnet download ../other-repo/docs/md
```

### `mdnet sync`

Downloads published docs for all referenced packages, as configured in [`mdnet.sources.json`](#consuming-dependency-docs-mdnetsourcesjson). It is safe to run repeatedly: unchanged docs are reported as up to date.

```
mdnet sync [<path>] [options]
```

| Argument / option | Default | Description |
|---|---|---|
| `<path>` | `.` | Solution, project or directory whose package references are synced. |
| `--config`, `-c` | `mdnet.sources.json` next to the solution/project, or in the directory | Sources file. |
| `--prune` | `false` | Delete downloaded docs for packages that are no longer referenced. |

```sh
mdnet sync MySolution.slnx
mdnet sync MySolution.slnx --prune
```

`sync` prints a warning when the published docs version differs from the referenced package version. It exits with `1` if any source failed.

## Output layout

`mdnet generate MySolution.slnx -o docs` writes:

```
docs/md/index.md                          entry point for agents: packages grouped by id prefix, with version and description
docs/md/MyLib/index.md                    frontmatter (version, description, authors, license, repository, frameworks, types),
                                          the project README, then namespaces (with their READMEs) and types, one line each
docs/md/MyLib/MyLib.Data/Repository-1.md  one page per type, in a folder per namespace (Repository<T> → Repository-1.md)
docs/md/MyLib/mdnet.json                  manifest: id, version, SHA-256 of every file
docs/html/…                               static site; a copy of the .md next to every page, llms.txt (= index.md)
```

`download` and `sync` write the same per-package folders (`<root>/<Id>/…`) and regenerate `<root>/index.md`.

A type page contains code, not tables. Each member shows its C# signature, followed by its XML doc comment:

````md
{% member id="addasync" %}
```csharp
public async Task<TEntity> AddAsync(TEntity entity, CancellationToken cancellationToken = default)
```

Asynchronously adds a new entity to the underlying database context.

* **entity**: The entity instance to be inserted.
* **cancellationToken**: A token to observe for cancellation requests.

**Returns**: The inserted entity with any database-generated values applied.

> **Remarks**: It does not automatically call [`SaveChangesAsync`](../Core.Data/DbContext.md#savechangesasync).
{% /member %}
````

## Writing docs that mdnet picks up

- **XML doc comments.** Supported tags: `summary`, `remarks`, `param`, `typeparam`, `returns`, `value`, `exception`, `example`, `code`, `c`, `see`, `seealso`, `paramref`, `typeparamref`, `list`, `para` and `inheritdoc`. `inheritdoc` can be explicit, including `cref`. Overrides and interface implementations without docs inherit them automatically. In source mode, comments are read from the source code. In package mode, set `<GenerateDocumentationFile>true</GenerateDocumentationFile>` in the library so that its package ships the `.xml` file.
- **Package metadata.** mdnet reads the evaluated project properties, including values inherited from `Directory.Build.props`: `PackageId`, `Version`, `Title`, `Description`, `Authors`, `Company`, `Copyright`, `PackageTags`, `PackageLicenseExpression`, `PackageProjectUrl`, `RepositoryUrl`, `RootNamespace` and the target frameworks. `Description` is the one-line summary shown in `index.md`.
- **Project README.** A `README.md` in the project directory is embedded in the package page. When there is no `Description`, its first paragraph is used as the summary. A title that only repeats the package name is removed, headings are demoted, and links relative to the repository are turned into plain text.
- **Namespace READMEs.** A `README.md` in a namespace folder is embedded in the package `index.md` under that namespace. Namespaces below `RootNamespace` map to sub folders (`MyLib.Domain.Entities` → `Domain/Entities/README.md`). This also works for folders that group features but contain no types of their own.

## Publishing docs

Every package folder (`docs/md/<Id>`) is self-contained and described by its `mdnet.json`. Publish it in either of these ways:

- **Static hosting.** The HTML site already contains `mdnet.json` and the `.md` sources, so `https://docs.example.com/MyLib/` works as a download source. Use a version in the path (`/MyLib/1.2.0/`) to keep old versions available.
- **Git.** Commit the folders (for example `docs/MyLib/`) and optionally tag releases (`v1.2.0`).

## Consuming dependency docs (`mdnet.sources.json`)

Place `mdnet.sources.json` next to the solution or project, then run `mdnet sync`.

```json
{
  "out": ".mdnet/docs",
  "sources": [
    { "packages": "Acme.*", "url": "https://docs.example.com/{id}/{version}/" },
    { "packages": "Foo.*", "git": "https://github.com/org/foo-docs", "ref": "v{version}", "path": "{id}" },
    { "packages": "Internal.*", "local": "../internal-docs/md" },
    { "git": "https://github.com/org/shared-docs" }
  ]
}
```

| Field | Description |
|---|---|
| `out` | Docs root, relative to the config file. Default: `.mdnet/docs`. |
| `sources[].packages` | Package id patterns, matched against the restored references of every project (transitive ones included). The highest referenced version is used. When omitted, every docs folder found at the source is downloaded. |
| `sources[].url` | HTTP(S) URL of a package docs folder. |
| `sources[].git` | Git repository URL. |
| `sources[].ref` | Git branch, tag or commit. Default: the repository's default branch. |
| `sources[].path` | Folder inside the repository or local directory that holds the docs. When omitted, the source is searched for the matching package's docs folder. |
| `sources[].local` | Local directory that holds package docs folders. |

Each source needs exactly one of `url`, `git` or `local`. The `url`, `git`, `ref`, `path` and `local` values can use the templates `{id}`, `{idLower}` and `{version}`. Comments and trailing commas are allowed in the file.

## Using mdnet with AI agents

**Point the agent at the index.** Add this to `AGENTS.md`, `CLAUDE.md` or `.github/copilot-instructions.md`:

```md
## API documentation
- Our libraries: `docs/md/index.md`
- Dependencies: `.mdnet/docs/index.md` (refresh with `dnx mdnet --yes -- sync`)

Read `index.md` first, then open only the type pages you need. Each type page lists C# signatures with their docs.
```

**Keep downloaded docs out of git.** Add this to `.gitignore`:

```gitignore
.mdnet/
```

**Refresh docs in CI or in a setup script:**

```sh
dotnet restore
dnx mdnet --yes -- sync MySolution.slnx --prune
```

**Recipes for agents:**

| Goal | Command |
|---|---|
| Docs for this repository, Markdown only (fast, no JS runtime) | `dnx mdnet --yes -- generate . -f md -o docs` |
| Docs for referenced packages that nobody has published | `dotnet restore` then `dnx mdnet --yes -- generate . -p "Vendor.*" -f md -o .mdnet/generated` |
| Download published docs for dependencies | `dnx mdnet --yes -- sync .` |
| Download one package's docs | `dnx mdnet --yes -- download <url-or-git-or-dir> -o .mdnet/docs` |
| Build an HTML site from downloaded docs | `dnx mdnet --yes -- render .mdnet/docs -o .mdnet/site` |

For hosted docs, the HTML site also serves `llms.txt` (a copy of `index.md`) and a `.md` file next to every `.html` page.

## Troubleshooting

| Symptom | Fix |
|---|---|
| `Nothing to document.` | The path contains no loadable non-test projects, or `--include`/`--exclude`/`--packages` matched nothing. Rerun with `-v`. |
| `No package references under … match the pattern.` | Check the `--packages`/`packages` pattern against the package ids the projects reference. Transitive references count too. |
| `… not found in the NuGet packages folder (run dotnet restore).` or `no project.assets.json (restore failed?)` | Run `dotnet restore` and fix any restore errors. Package mode and `sync` read `obj/project.assets.json` and try a restore themselves when it is missing. |
| `HTML rendering needs bun or node on PATH.` | Install `bun` or `node` and put it on `PATH`, choose one with `--runtime`, or skip HTML with `-f md`. |
| Projects fail to load | Make sure the .NET 10 SDK is installed and `dotnet build` succeeds for the project. |
| Git source fails | Make sure `git` is on `PATH` and the repository or `--ref` is accessible with your git credentials. |
| `mdnet.sources.json not found` | Create the file next to the solution, or pass `--config <file>`. |
| `dnx` asks for confirmation or swallows `--help` | Use `dnx mdnet --yes -- <command> …`. |

## Contributing

See [CONTRIBUTING.md](https://github.com/Hookyns/mdnet/blob/main/CONTRIBUTING.md).

## License

[AGPL-3.0](https://github.com/Hookyns/mdnet/blob/main/LICENSE)
