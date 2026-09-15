using System.CommandLine;
using Mdnet.Publishing;

namespace Mdnet.Cli;

internal static class DownloadCommand
{
    public static Command Create()
    {
        var source = new Argument<string>("source")
        {
            Description = "HTTP(S) URL of a docs folder (containing mdnet.json), a git repository URL, or a local directory.",
        };
        var gitRef = new Option<string?>("--ref") { Description = "Git branch, tag or commit." };
        var path = new Option<string[]>("--path")
        {
            Description = "Folder(s) inside the git repository holding package docs. Default: every docs folder in the repository.",
            AllowMultipleArgumentsPerToken = true,
        };
        var git = new Option<bool>("--git") { Description = "Treat the source as a git repository even if the URL does not look like one." };
        var output = new Option<string>("--output", "-o")
        {
            Description = "Docs root; each package goes to <output>/<id>.",
            DefaultValueFactory = _ => ".mdnet/docs",
        };

        var command = new Command("download", "Download published docs into a local docs root.") { source, gitRef, path, git, output }.WithLogging();
        command.SetAction(
            (result, cancellationToken) =>
            {
                var log = CommonOptions.CreateLog(result);
                return CommonOptions.RunAsync(
                    log,
                    async () =>
                    {
                        var root = Path.GetFullPath(result.GetValue(output)!);
                        var location = result.GetValue(source)!;
                        var paths = result.GetValue(path) ?? [];
                        var refValue = result.GetValue(gitRef);
                        using var http = CreateHttpClient();
                        using var checkouts = new GitCheckouts(log);

                        IReadOnlyList<IDocsSource> sources;
                        if (Directory.Exists(location))
                        {
                            sources = DirectoryDocsSource.FindDocsFolders(location).Select(d => new DirectoryDocsSource(d)).ToList();
                        }
                        else if (result.GetValue(git) || refValue is not null || paths.Length > 0 || LooksLikeGit(location))
                        {
                            var checkout = await checkouts.CheckoutAsync(location, refValue, paths, cancellationToken);
                            sources = (paths.Length > 0 ? paths.Select(p => Path.Combine(checkout, p)) : [checkout])
                                .Where(Directory.Exists)
                                .SelectMany(d => DirectoryDocsSource.FindDocsFolders(d))
                                .Select(d => new DirectoryDocsSource(d))
                                .ToList();
                        }
                        else if (Uri.TryCreate(location, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https")
                        {
                            sources = [new HttpDocsSource(http, uri)];
                        }
                        else
                        {
                            throw new ArgumentException($"Unrecognized source: {location}");
                        }

                        var installed = 0;
                        foreach (var docs in sources)
                        {
                            var (status, manifest) = await DocsInstaller.InstallAsync(docs, root, log, cancellationToken);
                            if (status == InstallResult.NotFound)
                            {
                                log.Warn($"No mdnet.json at {docs.Description}");
                                continue;
                            }

                            installed++;
                            log.Info($"{manifest!.Id} {manifest.Version}: {(status == InstallResult.UpToDate ? "up to date" : "downloaded")}");
                        }

                        if (installed == 0)
                        {
                            log.Error("No docs found.");
                            return 1;
                        }

                        RootIndex.Write(root);
                        return 0;
                    }
                );
            }
        );
        return command;
    }

    public static HttpClient CreateHttpClient()
    {
        var http = new HttpClient { Timeout = TimeSpan.FromMinutes(2) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("mdnet/" + typeof(DownloadCommand).Assembly.GetName().Version?.ToString(3));
        return http;
    }

    public static bool LooksLikeGit(string location) =>
        location.EndsWith(".git", StringComparison.OrdinalIgnoreCase)
        || location.StartsWith("git@", StringComparison.Ordinal)
        || location.StartsWith("ssh://", StringComparison.Ordinal)
        || location.StartsWith("git://", StringComparison.Ordinal)
        || (Uri.TryCreate(location, UriKind.Absolute, out var uri)
            && uri.Host is "github.com" or "gitlab.com" or "bitbucket.org"
            && uri.AbsolutePath.Trim('/').Count(c => c == '/') == 1);
}
