using System.CommandLine;
using Mdnet.Loading;
using Mdnet.Publishing;

namespace Mdnet.Cli;

internal static class SyncCommand
{
    public static Command Create()
    {
        var path = new Argument<string>("path")
        {
            Description = "Solution, project or directory whose package references are synced.",
            DefaultValueFactory = _ => ".",
        };
        var config = new Option<string?>("--config", "-c")
        {
            Description = $"Sources file. Default: {SourcesConfig.FileName} next to the solution/project or in the directory.",
        };
        var prune = new Option<bool>("--prune") { Description = "Delete downloaded docs of packages that are no longer referenced." };

        var command = new Command("sync", $"Download published docs for referenced packages, as configured in {SourcesConfig.FileName}.")
        {
            path,
            config,
            prune,
        }.WithLogging();

        command.SetAction(
            (result, cancellationToken) =>
            {
                var log = CommonOptions.CreateLog(result);
                return CommonOptions.RunAsync(log, () => RunAsync(result.GetValue(path)!, result.GetValue(config), result.GetValue(prune), log, cancellationToken));
            }
        );
        return command;
    }

    private static async Task<int> RunAsync(string target, string? configPath, bool prune, Log log, CancellationToken cancellationToken)
    {
        var fullTarget = Path.GetFullPath(target);
        var baseDir = Directory.Exists(fullTarget) ? fullTarget : Path.GetDirectoryName(fullTarget)!;
        configPath = Path.GetFullPath(configPath ?? Path.Combine(baseDir, SourcesConfig.FileName));
        if (!File.Exists(configPath))
        {
            throw new FileNotFoundException($"{configPath} not found. Create it, e.g.:\n{ExampleConfig}");
        }

        var config = SourcesConfig.Load(configPath);
        var root = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(configPath)!, config.Out));
        using var http = DownloadCommand.CreateHttpClient();
        using var checkouts = new GitCheckouts(log);

        var synced = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var failures = 0;
        foreach (var entry in config.Sources)
        {
            var packages = entry.Packages is null
                ? [null]
                : (await PackageLoader.FindAsync(target, new Glob([entry.Packages]), log, cancellationToken)).Select(m => (RestoredPackage?)m.Package).ToList();
            if (packages.Count == 0)
            {
                log.Verbose($"No references match {entry.Packages}.");
            }

            foreach (var package in packages)
            {
                string Expand(string template) => package is null ? template : SourcesConfig.Expand(template, package.Id, package.Version);

                try
                {
                    var sources = await ResolveSourcesAsync(entry, package?.Id, Expand, http, checkouts, cancellationToken);
                    var label = package is null ? entry.Url ?? entry.Git ?? entry.Local : $"{package.Id} {package.Version}";
                    if (sources.Count == 0)
                    {
                        log.Warn($"{label}: no published docs found.");
                        continue;
                    }

                    foreach (var source in sources)
                    {
                        var (status, manifest) = await DocsInstaller.InstallAsync(source, root, log, cancellationToken);
                        if (status == InstallResult.NotFound)
                        {
                            log.Warn($"{label}: no mdnet.json at {source.Description}");
                            continue;
                        }

                        synced.Add(manifest!.Id);
                        if (package is not null && manifest.Version != package.Version)
                        {
                            log.Warn($"{manifest.Id}: published docs are {manifest.Version}, project references {package.Version}.");
                        }

                        log.Info($"{manifest.Id} {manifest.Version}: {(status == InstallResult.UpToDate ? "up to date" : "updated")}");
                    }
                }
                catch (Exception e) when (e is HttpRequestException or InvalidOperationException or InvalidDataException or IOException)
                {
                    failures++;
                    log.Error($"{package?.Id ?? entry.Url ?? entry.Git}: {e.Message}");
                }
            }
        }

        if (prune && Directory.Exists(root))
        {
            foreach (var dir in Directory.EnumerateDirectories(root))
            {
                if (File.Exists(Path.Combine(dir, Manifest.FileName)) && !synced.Contains(Path.GetFileName(dir)))
                {
                    log.Info($"Pruning {Path.GetFileName(dir)}");
                    Directory.Delete(dir, recursive: true);
                }
            }
        }

        RootIndex.Write(root);
        log.Info($"Docs: {root}");
        return failures == 0 ? 0 : 1;
    }

    private static async Task<IReadOnlyList<IDocsSource>> ResolveSourcesAsync(
        DocsSourceEntry entry,
        string? packageId,
        Func<string, string> expand,
        HttpClient http,
        GitCheckouts checkouts,
        CancellationToken cancellationToken
    )
    {
        if (entry.Url is not null)
        {
            return [new HttpDocsSource(http, new Uri(expand(entry.Url)))];
        }

        string baseDir;
        string? subPath = entry.Path is null ? null : expand(entry.Path);
        if (entry.Git is not null)
        {
            var gitRef = entry.Ref is null ? null : expand(entry.Ref);
            baseDir = await checkouts.CheckoutAsync(expand(entry.Git), gitRef, subPath is null ? [] : [subPath], cancellationToken);
        }
        else
        {
            baseDir = Path.GetFullPath(expand(entry.Local!));
        }

        var dir = subPath is null ? baseDir : Path.Combine(baseDir, subPath);
        if (!Directory.Exists(dir))
        {
            return [];
        }

        if (subPath is not null && File.Exists(Path.Combine(dir, Manifest.FileName)))
        {
            return [new DirectoryDocsSource(dir)];
        }

        // No explicit folder: search, and for a specific package keep only its own docs folder.
        return DirectoryDocsSource.FindDocsFolders(dir)
            .Where(d => packageId is null || string.Equals(Manifest.TryLoad(d)?.Id, packageId, StringComparison.OrdinalIgnoreCase))
            .Select(d => (IDocsSource)new DirectoryDocsSource(d))
            .ToList();
    }

    private const string ExampleConfig = """
        {
          "out": ".mdnet/docs",
          "sources": [
            { "packages": "Acme.*", "url": "https://docs.example.com/{id}/{version}/" },
            { "packages": "Foo.*", "git": "https://github.com/org/foo-docs", "ref": "v{version}", "path": "{id}" }
          ]
        }
        """;
}
