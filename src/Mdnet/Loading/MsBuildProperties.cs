using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Mdnet.Model;

namespace Mdnet.Loading;

/// <summary>Package identity and metadata of a project.</summary>
public sealed record ProjectPackageInfo(string? Id, string? Version, PackageMetadata Metadata, string? RootNamespace = null);

/// <summary>
/// Reads package properties from the evaluated project (<c>dotnet msbuild -getProperty</c>), so values inherited from
/// <c>Directory.Build.props</c> and SDK defaults are applied. Falls back to the raw project XML.
/// </summary>
public static partial class MsBuildProperties
{
    private static readonly string[] Names =
    [
        "PackageId",
        "Version",
        "AssemblyName",
        "RootNamespace",
        "Title",
        "Description",
        "Authors",
        "Company",
        "Copyright",
        "PackageTags",
        "PackageLicenseExpression",
        "PackageProjectUrl",
        "RepositoryUrl",
        "TargetFramework",
        "TargetFrameworks",
    ];

    public static async Task<IReadOnlyDictionary<string, ProjectPackageInfo>> ReadAsync(
        IReadOnlyList<string> projects,
        Log log,
        CancellationToken cancellationToken
    )
    {
        using var throttle = new SemaphoreSlim(Math.Max(2, Environment.ProcessorCount / 2));
        var results = await Task.WhenAll(
            projects.Select(async project =>
            {
                await throttle.WaitAsync(cancellationToken);
                try
                {
                    return (project, info: await ReadAsync(project, log, cancellationToken));
                }
                finally
                {
                    throttle.Release();
                }
            })
        );
        return results.ToDictionary(r => r.project, r => r.info, StringComparer.OrdinalIgnoreCase);
    }

    private static async Task<ProjectPackageInfo> ReadAsync(string project, Log log, CancellationToken cancellationToken)
    {
        var arguments = new List<string> { "msbuild", project, "-nologo" };
        arguments.AddRange(Names.Select(n => $"-getProperty:{n}"));
        try
        {
            var result = await ProcessRunner.RunAsync("dotnet", arguments, Path.GetDirectoryName(project), cancellationToken);
            if (result.Success)
            {
                using var json = JsonDocument.Parse(result.Output);
                var properties = json.RootElement.GetProperty("Properties")
                    .EnumerateObject()
                    .ToDictionary(p => p.Name, p => p.Value.GetString(), StringComparer.OrdinalIgnoreCase);
                return FromProperties(name => properties.GetValueOrDefault(name));
            }

            log.Verbose($"{Path.GetFileName(project)}: msbuild property evaluation failed; reading the project file. {result.Error.Trim()}");
        }
        catch (Exception e) when (e is JsonException or KeyNotFoundException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            log.Verbose($"{Path.GetFileName(project)}: msbuild property evaluation failed ({e.Message}); reading the project file.");
        }

        return FromProjectFile(project);
    }

    /// <summary>Raw project XML: only properties set directly in the file, without imports.</summary>
    public static ProjectPackageInfo FromProjectFile(string project)
    {
        try
        {
            var doc = XDocument.Load(project);
            return FromProperties(name =>
                doc.Descendants().LastOrDefault(e => e.Name.LocalName == name && e.Parent?.Name.LocalName == "PropertyGroup")?.Value is { } v
                && !v.Contains("$(", StringComparison.Ordinal)
                    ? v
                    : null
            );
        }
        catch (System.Xml.XmlException)
        {
            return new ProjectPackageInfo(null, null, PackageMetadata.Empty);
        }
    }

    private static ProjectPackageInfo FromProperties(Func<string, string?> get)
    {
        string? Value(string name) => Normalize(get(name));

        var assemblyName = Value("AssemblyName");
        var id = Value("PackageId") ?? assemblyName;
        // SDK defaults carry no information: Version=1.0.0, Authors/Company/Title=AssemblyName, Company=Authors.
        string? NotDefault(string? value, params string?[] defaults) =>
            value is null || defaults.Any(d => string.Equals(d, value, StringComparison.Ordinal)) ? null : value;

        var authors = NotDefault(Value("Authors"), assemblyName, id);
        var frameworks = (Value("TargetFrameworks") ?? Value("TargetFramework") ?? "")
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .OrderByDescending(AssetsFile.FrameworkRank)
            .ToList();

        var metadata = new PackageMetadata(
            Title: NotDefault(Value("Title"), assemblyName, id),
            Description: NotDefault(Value("Description"), "Package Description"),
            Authors: authors,
            Company: NotDefault(Value("Company"), assemblyName, id, authors),
            Copyright: Value("Copyright"),
            Tags: NormalizeTags(Value("PackageTags")),
            License: Value("PackageLicenseExpression"),
            ProjectUrl: Value("PackageProjectUrl"),
            RepositoryUrl: Value("RepositoryUrl"),
            Frameworks: frameworks
        );
        return new ProjectPackageInfo(id, NotDefault(Value("Version"), "1.0.0"), metadata, Value("RootNamespace") ?? assemblyName);
    }

    /// <summary>Tags as <c>a, b</c>: projects separate them with <c>;</c>, nuspecs with spaces.</summary>
    public static string? NormalizeTags(string? tags) =>
        tags?.Split([';', ',', ' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries) is { Length: > 0 } parts ? string.Join(", ", parts) : null;

    /// <summary>Collapses whitespace (descriptions often span indented XML lines); empty becomes null.</summary>
    public static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : Whitespace().Replace(value.Trim(), " ");

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
