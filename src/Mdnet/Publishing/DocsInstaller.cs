namespace Mdnet.Publishing;

public enum InstallResult
{
    NotFound,
    UpToDate,
    Updated,
}

/// <summary>Copies a published docs folder into <c>&lt;root&gt;/&lt;id&gt;</c>, fetching only changed files.</summary>
public static class DocsInstaller
{
    public static async Task<(InstallResult Result, Manifest? Manifest)> InstallAsync(
        IDocsSource source,
        string root,
        Log log,
        CancellationToken cancellationToken
    )
    {
        var remote = await source.ReadManifestAsync(cancellationToken);
        if (remote is null)
        {
            return (InstallResult.NotFound, null);
        }

        if (!Manifest.IsSafeRelativePath(remote.Id) || remote.Id.Contains('/'))
        {
            throw new InvalidDataException($"Invalid package id in {source.Description}: {remote.Id}");
        }

        var target = Path.Combine(root, remote.Id);
        Manifest? local;
        try
        {
            local = Manifest.TryLoad(target);
        }
        catch (Exception e) when (e is InvalidDataException or System.Text.Json.JsonException)
        {
            local = null;
        }

        if (
            local is not null
            && local.Version == remote.Version
            && local.Files.Count == remote.Files.Count
            && remote.Files.All(f => local.Files.GetValueOrDefault(f.Key) == f.Value && DiskHash(Path.Combine(target, f.Key)) == f.Value)
        )
        {
            return (InstallResult.UpToDate, local);
        }

        var files = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        var fetched = 0;
        foreach (var (path, hash) in remote.Files)
        {
            var localPath = Path.Combine(target, path);
            if (local?.Files.GetValueOrDefault(path) == hash && File.Exists(localPath))
            {
                var existing = await File.ReadAllBytesAsync(localPath, cancellationToken);
                if (Manifest.Hash(existing) == hash)
                {
                    files[path] = existing;
                    continue;
                }
            }

            var content = await source.ReadFileAsync(path, cancellationToken);
            if (Manifest.Hash(content) != hash)
            {
                throw new InvalidDataException($"{source.Description}: {path} does not match its mdnet.json hash.");
            }

            files[path] = content;
            fetched++;
        }

        // Force rewrites of locally modified files: DocsFolder skips files whose previous manifest hash matches.
        foreach (var (path, _) in remote.Files)
        {
            if (local?.Files.ContainsKey(path) == true && DiskHash(Path.Combine(target, path)) != Manifest.Hash(files[path]))
            {
                File.Delete(Path.Combine(target, path));
            }
        }

        DocsFolder.Replace(target, files, remote with { Files = new(StringComparer.Ordinal) });
        log.Verbose($"{remote.Id}: fetched {fetched} of {remote.Files.Count} file(s)");
        return (InstallResult.Updated, remote);
    }

    private static string? DiskHash(string path) => File.Exists(path) ? Manifest.Hash(File.ReadAllBytes(path)) : null;
}
