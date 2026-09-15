using System.Text.Json;
using System.Text.Json.Serialization;

namespace Mdnet.Publishing;

/// <summary>
/// <c>mdnet.sources.json</c>: where published docs for referenced packages live.
/// Templates support <c>{id}</c>, <c>{idLower}</c> and <c>{version}</c>.
/// </summary>
public sealed record SourcesConfig
{
    public const string FileName = "mdnet.sources.json";

    /// <summary>Docs root, relative to the config file.</summary>
    public string Out { get; init; } = ".mdnet/docs";

    public IReadOnlyList<DocsSourceEntry> Sources { get; init; } = [];

    public static SourcesConfig Load(string path)
    {
        var config = JsonSerializer.Deserialize(File.ReadAllText(path), SourcesConfigJsonContext.Default.SourcesConfig)
            ?? throw new InvalidDataException($"{path} is empty.");
        foreach (var source in config.Sources)
        {
            if ((source.Url is null) == (source.Git is null) && source.Local is null)
            {
                throw new InvalidDataException($"{path}: each source needs exactly one of \"url\", \"git\" or \"local\".");
            }
        }

        return config;
    }

    public static string Expand(string template, string id, string? version) =>
        template
            .Replace("{id}", id, StringComparison.Ordinal)
            .Replace("{idLower}", id.ToLowerInvariant(), StringComparison.Ordinal)
            .Replace("{version}", version ?? "", StringComparison.Ordinal);
}

/// <param name="Packages">Package id pattern(s) resolved against the projects' restored references. When omitted, every docs folder found at the source is downloaded.</param>
/// <param name="Url">HTTP(S) base URL of a package docs folder, e.g. <c>https://docs.example.com/{id}/{version}/</c>.</param>
/// <param name="Git">Git repository URL.</param>
/// <param name="Ref">Branch, tag or commit, e.g. <c>v{version}</c>. Defaults to the repository's default branch.</param>
/// <param name="Path">Folder inside the repository (git) holding the package docs, e.g. <c>docs/{id}</c>.</param>
/// <param name="Local">Local directory holding package docs folders.</param>
public sealed record DocsSourceEntry(string? Packages, string? Url, string? Git, string? Ref, string? Path, string? Local);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true)]
[JsonSerializable(typeof(SourcesConfig))]
internal sealed partial class SourcesConfigJsonContext : JsonSerializerContext;
