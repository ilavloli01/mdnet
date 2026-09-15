using Mdnet.Loading;

namespace Mdnet.Publishing;

/// <summary>Somewhere a published package docs folder (with <c>mdnet.json</c>) can be read from.</summary>
public interface IDocsSource
{
    string Description { get; }

    Task<Manifest?> ReadManifestAsync(CancellationToken cancellationToken);

    Task<byte[]> ReadFileAsync(string path, CancellationToken cancellationToken);
}

/// <summary>A docs folder served over HTTP(S): <c>{base}/mdnet.json</c>, <c>{base}/{file}</c>.</summary>
public sealed class HttpDocsSource(HttpClient http, Uri baseUri) : IDocsSource
{
    private readonly Uri _base = baseUri.AbsoluteUri.EndsWith('/') ? baseUri : new Uri(baseUri.AbsoluteUri + "/");

    public string Description => _base.AbsoluteUri;

    public async Task<Manifest?> ReadManifestAsync(CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync(new Uri(_base, Manifest.FileName), cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return Manifest.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
    }

    public Task<byte[]> ReadFileAsync(string path, CancellationToken cancellationToken) =>
        http.GetByteArrayAsync(new Uri(_base, string.Join("/", path.Split('/').Select(Uri.EscapeDataString))), cancellationToken);
}

/// <summary>A docs folder on disk (a local path or a git checkout).</summary>
public sealed class DirectoryDocsSource(string directory) : IDocsSource
{
    public string Description => directory;

    public Task<Manifest?> ReadManifestAsync(CancellationToken cancellationToken)
    {
        var path = Path.Combine(directory, Manifest.FileName);
        return Task.FromResult(File.Exists(path) ? Manifest.Parse(File.ReadAllText(path)) : null);
    }

    public Task<byte[]> ReadFileAsync(string path, CancellationToken cancellationToken) =>
        File.ReadAllBytesAsync(Path.Combine(directory, path.Replace('/', Path.DirectorySeparatorChar)), cancellationToken);

    /// <summary>Package docs folders at or below <paramref name="root"/> (not inside other docs folders).</summary>
    public static IReadOnlyList<string> FindDocsFolders(string root, int maxDepth = 4)
    {
        var found = new List<string>();
        Walk(root, 0);
        return found;

        void Walk(string dir, int depth)
        {
            if (File.Exists(Path.Combine(dir, Manifest.FileName)))
            {
                found.Add(dir);
                return;
            }

            if (depth >= maxDepth)
            {
                return;
            }

            foreach (var child in Directory.EnumerateDirectories(dir).Where(d => !Path.GetFileName(d).StartsWith('.')))
            {
                Walk(child, depth + 1);
            }
        }
    }
}

/// <summary>Shallow, sparse clones of git repositories into temporary folders.</summary>
public sealed class GitCheckouts(Log log) : IDisposable
{
    private readonly Dictionary<string, string> _checkouts = new(StringComparer.Ordinal);

    /// <summary>Checks out <paramref name="paths"/> (or the whole tree) of <paramref name="repository"/> at <paramref name="gitRef"/>.</summary>
    public async Task<string> CheckoutAsync(string repository, string? gitRef, IReadOnlyCollection<string> paths, CancellationToken cancellationToken)
    {
        var key = $"{repository}#{gitRef}#{string.Join("|", paths.Order(StringComparer.Ordinal))}";
        if (_checkouts.TryGetValue(key, out var existing))
        {
            return existing;
        }

        if (ProcessRunner.FindOnPath("git") is null)
        {
            throw new InvalidOperationException("git was not found on PATH.");
        }

        var dir = Path.Combine(Path.GetTempPath(), "mdnet-git-" + Guid.NewGuid().ToString("N")[..12]);
        log.Info($"Cloning {repository}{(gitRef is null ? "" : "@" + gitRef)}");

        var isCommit = gitRef is { Length: >= 7 and <= 40 } && gitRef.All(Uri.IsHexDigit);
        var sparse = paths.Count > 0;
        // Manifest hashes are over the exact published bytes: never let git rewrite line endings.
        List<string> clone = ["-c", "core.autocrlf=false", "clone", "--config", "core.autocrlf=false", "--config", "core.eol=lf", "--depth", "1", "--filter=blob:none", "--no-checkout"];
        if (gitRef is not null && !isCommit)
        {
            clone.AddRange(["--branch", gitRef]);
        }

        clone.AddRange([repository, dir]);
        await Git(clone, null, cancellationToken);
        if (isCommit)
        {
            await Git(["fetch", "--depth", "1", "--filter=blob:none", "origin", gitRef!], dir, cancellationToken);
        }

        if (sparse)
        {
            await Git(["sparse-checkout", "set", "--no-cone", .. paths.Select(p => "/" + p.Trim('/') + "/")], dir, cancellationToken);
        }

        await Git(["checkout", isCommit ? "FETCH_HEAD" : "HEAD"], dir, cancellationToken);
        _checkouts[key] = dir;
        return dir;
    }

    private static async Task Git(IEnumerable<string> arguments, string? workingDirectory, CancellationToken cancellationToken)
    {
        var args = arguments.ToList();
        var result = await ProcessRunner.RunAsync("git", args, workingDirectory, cancellationToken);
        if (!result.Success)
        {
            throw new InvalidOperationException($"git {args[0]} failed: {result.Error.Trim()}");
        }
    }

    public void Dispose()
    {
        foreach (var dir in _checkouts.Values)
        {
            try
            {
                foreach (var file in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
                {
                    File.SetAttributes(file, FileAttributes.Normal);
                }

                Directory.Delete(dir, recursive: true);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // Temp folder cleanup is best effort.
            }
        }
    }
}
