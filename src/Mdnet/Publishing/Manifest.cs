using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Mdnet.Publishing;

/// <summary>
/// <c>mdnet.json</c> at the root of each package docs folder. Lists every file with its SHA-256 so
/// downloads can skip unchanged files and remove stale ones.
/// </summary>
public sealed record Manifest(string Id, string? Version)
{
    public const string FileName = "mdnet.json";
    public const int CurrentSchema = 1;

    public int Schema { get; init; } = CurrentSchema;

    /// <summary>Path relative to the package folder (forward slashes) → lowercase hex SHA-256.</summary>
    public SortedDictionary<string, string> Files { get; init; } = new(StringComparer.Ordinal);

    public string ToJson() =>
        JsonSerializer.Serialize(
            new ManifestJson(Schema, Id, Version, Files),
            ManifestJsonContext.Default.ManifestJson
        ) + "\n";

    public static Manifest Parse(string json)
    {
        var data = JsonSerializer.Deserialize(json, ManifestJsonContext.Default.ManifestJson)
            ?? throw new InvalidDataException("Empty mdnet.json.");
        if (data.Schema > CurrentSchema)
        {
            throw new InvalidDataException($"mdnet.json schema {data.Schema} is newer than this mdnet supports ({CurrentSchema}). Update mdnet.");
        }

        if (string.IsNullOrWhiteSpace(data.Id))
        {
            throw new InvalidDataException("mdnet.json has no id.");
        }

        foreach (var path in data.Files.Keys)
        {
            if (!IsSafeRelativePath(path))
            {
                throw new InvalidDataException($"mdnet.json contains an unsafe path: {path}");
            }
        }

        return new Manifest(data.Id, data.Version)
        {
            Schema = data.Schema,
            Files = new SortedDictionary<string, string>(data.Files, StringComparer.Ordinal),
        };
    }

    public static Manifest? TryLoad(string directory)
    {
        var path = Path.Combine(directory, FileName);
        return File.Exists(path) ? Parse(File.ReadAllText(path)) : null;
    }

    public static string Hash(byte[] content) => Convert.ToHexStringLower(SHA256.HashData(content));

    /// <summary>Rejects absolute paths, <c>..</c> segments and backslashes so remote manifests cannot escape the target folder.</summary>
    public static bool IsSafeRelativePath(string path) =>
        path.Length > 0
        && !path.Contains('\\')
        && !path.StartsWith('/')
        && !path.Contains(':')
        && path.Split('/').All(segment => segment.Length > 0 && segment != "." && segment != "..");
}

internal sealed record ManifestJson(
    int Schema,
    string Id,
    string? Version,
    SortedDictionary<string, string> Files
);

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
)]
[JsonSerializable(typeof(ManifestJson))]
internal sealed partial class ManifestJsonContext : JsonSerializerContext;
