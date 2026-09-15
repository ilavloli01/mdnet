# mdnet

AI-friendly API documentation for .NET libraries.

`mdnet` reads C# source or NuGet packages with Roslyn and writes **signature-first Markdown** that agents read directly. It renders the same Markdown into a clean static HTML site with [Markdoc](https://markdoc.dev), React and [Shiki](https://shiki.style). It can also **download docs that others published** (over HTTP or from a git repo), so an agent can read docs for your dependencies without generating them.

```sh
dotnet tool install -g mdnet
```

## Generate

```sh
# From source: a solution (.sln/.slnx), a project, or a directory
mdnet generate MySolution.slnx -o docs

# From referenced NuGet packages (read from the NuGet packages folder)
mdnet generate MySolution.slnx --packages "Acme.*" -o docs/deps

# Markdown only (no JavaScript runtime needed)
mdnet generate src/MyLib -f md
```

Output:

```
docs/md/index.md                         entry point for agents: packages grouped by id prefix, version, description
docs/md/MyLib/index.md                   frontmatter (version, description, authors, license, repository, frameworks, types)
                                         + namespaces and types, one line each
docs/md/MyLib/MyLib.Data/Repository-1.md one page per type
docs/md/MyLib/mdnet.json                 manifest (id, version, file hashes)
docs/html/…                              static site (+ .md next to every page, llms.txt)
```

A type page is code, not tables. Each member shows its C# signature followed by its XML doc comment:

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

| Option | Default | |
|---|---|---|
| `--packages`, `-p` | – | Package mode: id patterns (`*`, `?`, `;`-separated, repeatable) |
| `--output`, `-o` | `docs` | Markdown goes to `<output>/md`, HTML to `<output>/html` |
| `--format`, `-f` | `md html` | Output formats |
| `--visibility` | `protected` | `public`, `protected` or `internal` |
| `--include` / `--exclude` | – / `*.Benchmarks *.Samples` | Source mode project name filters (test projects are always skipped) |
| `--runtime` | `auto` | `bun` or `node` for HTML rendering |

Supported doc comment tags: `summary`, `remarks`, `param`, `typeparam`, `returns`, `value`, `exception`, `example`, `code`, `c`, `see`, `seealso`, `paramref`, `typeparamref`, `list`, `para`, and `inheritdoc` (explicit, including `cref`; overrides and interface implementations without docs inherit automatically).

## Publish

Every package folder (`docs/md/<Id>`) is self-contained and described by its `mdnet.json`. Publish it anywhere:

* **Static hosting**: the HTML site already contains `mdnet.json` and the `.md` sources, so `https://docs.example.com/MyLib/` is a download source.
* **Git**: commit the folders (for example `docs/MyLib/`), and optionally tag releases (`v1.2.0`).

## Download and sync

```sh
# One-off
mdnet download https://docs.example.com/Acme.Core/
mdnet download https://github.com/org/acme-docs --ref v1.2.0 --path docs/Acme.Core
mdnet download ../other-repo/docs/md

# Every referenced package, as configured in mdnet.sources.json
mdnet sync MySolution.slnx
```

`mdnet.sources.json` (next to the solution):

```json
{
  "out": ".mdnet/docs",
  "sources": [
    { "packages": "Acme.*", "url": "https://docs.example.com/{id}/{version}/" },
    { "packages": "Foo.*", "git": "https://github.com/org/foo-docs", "ref": "v{version}", "path": "{id}" },
    { "git": "https://github.com/org/shared-docs" }
  ]
}
```

* `packages` is matched against the restored references of every project (transitive ones included), and the highest referenced version is used. Templates can use `{id}`, `{idLower}` and `{version}`. Without `packages`, every docs folder found at the source is downloaded.
* Only changed files are fetched, and every file is checked against its manifest hash. `--prune` removes docs for packages that are no longer referenced.
* `.mdnet/docs/index.md` lists everything downloaded. Point your agent at it, for example from `AGENTS.md`: *"API docs for dependencies: `.mdnet/docs/index.md`."*

## Render

```sh
mdnet render .mdnet/docs -o .mdnet/site
```

## Requirements

* .NET 10 SDK or newer (the tool runs on .NET 10+ and `generate` loads projects through MSBuild).
* `bun` or `node` on PATH for HTML output. The renderer ships inside the tool as a single bundled file, so nothing is installed from npm at runtime.
* `git` on PATH for git sources.

## Development

```sh
cd renderer && bun install && bun run build && cd ..
dotnet test --project tests/Mdnet.Tests          # MDNET_ACCEPT=1 accepts snapshot changes
dotnet run --project src/Mdnet -- generate samples/Sample.Lib -o out
dotnet pack src/Mdnet -c Release                 # → nupkg/
```
