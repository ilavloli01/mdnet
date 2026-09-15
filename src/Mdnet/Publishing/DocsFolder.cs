using System.Text;

namespace Mdnet.Publishing;

/// <summary>Writes a package docs folder so that it exactly matches a file set, then writes its manifest.</summary>
public static class DocsFolder
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    public static Manifest Replace(string directory, IReadOnlyDictionary<string, string> textFiles, Manifest manifest) =>
        Replace(directory, textFiles.ToDictionary(f => f.Key, f => Utf8NoBom.GetBytes(f.Value), StringComparer.Ordinal), manifest);

    public static Manifest Replace(string directory, IReadOnlyDictionary<string, byte[]> files, Manifest manifest)
    {
        Directory.CreateDirectory(directory);
        var previous = TryLoadQuietly(directory);
        var result = manifest with { Files = new SortedDictionary<string, string>(StringComparer.Ordinal) };

        foreach (var (relative, content) in files)
        {
            if (!Manifest.IsSafeRelativePath(relative))
            {
                throw new InvalidDataException($"Unsafe docs path: {relative}");
            }

            var hash = Manifest.Hash(content);
            result.Files[relative] = hash;
            var path = Path.Combine(directory, relative);
            if (previous?.Files.GetValueOrDefault(relative) == hash && File.Exists(path))
            {
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, content);
        }

        if (previous is not null)
        {
            foreach (var stale in previous.Files.Keys.Where(k => !result.Files.ContainsKey(k)))
            {
                var path = Path.Combine(directory, stale);
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }

            RemoveEmptyDirectories(directory);
        }

        File.WriteAllText(Path.Combine(directory, Manifest.FileName), result.ToJson(), Utf8NoBom);
        return result;
    }

    private static Manifest? TryLoadQuietly(string directory)
    {
        try
        {
            return Manifest.TryLoad(directory);
        }
        catch (Exception e) when (e is InvalidDataException or System.Text.Json.JsonException)
        {
            return null;
        }
    }

    private static void RemoveEmptyDirectories(string root)
    {
        foreach (var dir in Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories).OrderByDescending(d => d.Length))
        {
            if (!Directory.EnumerateFileSystemEntries(dir).Any())
            {
                Directory.Delete(dir);
            }
        }
    }
}
