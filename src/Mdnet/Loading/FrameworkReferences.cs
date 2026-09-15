using System.Runtime.InteropServices;

namespace Mdnet.Loading;

/// <summary>Locates reference assemblies of shared frameworks (Microsoft.NETCore.App, Microsoft.AspNetCore.App, …).</summary>
public static class FrameworkReferences
{
    public static IReadOnlyList<string> Find(string targetFramework, IEnumerable<string> frameworks, Log log)
    {
        var dotnetRoot = DotnetRoot();
        var result = new List<string>();
        foreach (var framework in frameworks)
        {
            var dir = RefPackDirectory(dotnetRoot, framework, targetFramework) ?? SharedRuntimeDirectory(dotnetRoot, framework);
            if (dir is null)
            {
                log.Verbose($"No reference assemblies found for {framework} ({targetFramework}).");
                continue;
            }

            log.Verbose($"Framework {framework}: {dir}");
            result.AddRange(Directory.EnumerateFiles(dir, "*.dll"));
        }

        return result;
    }

    private static string DotnetRoot() =>
        Environment.GetEnvironmentVariable("DOTNET_ROOT") is { Length: > 0 } root && Directory.Exists(Path.Combine(root, "packs"))
            ? root
            : Path.GetFullPath(Path.Combine(RuntimeEnvironment.GetRuntimeDirectory(), "..", "..", ".."));

    private static string? RefPackDirectory(string dotnetRoot, string framework, string targetFramework)
    {
        var packs = Path.Combine(dotnetRoot, "packs", framework + ".Ref");
        if (!Directory.Exists(packs))
        {
            return null;
        }

        var candidates = Directory.EnumerateDirectories(packs)
            .OrderByDescending(d => ParseVersion(Path.GetFileName(d)))
            .SelectMany(d => Directory.Exists(Path.Combine(d, "ref")) ? Directory.EnumerateDirectories(Path.Combine(d, "ref")) : [])
            .ToList();
        var tfm = targetFramework.Split('-')[0];
        return candidates.FirstOrDefault(c => Path.GetFileName(c) == tfm)
            ?? candidates.OrderByDescending(c => AssetsFile.FrameworkRank(Path.GetFileName(c))).FirstOrDefault();
    }

    private static string? SharedRuntimeDirectory(string dotnetRoot, string framework)
    {
        var shared = Path.Combine(dotnetRoot, "shared", framework);
        return Directory.Exists(shared)
            ? Directory.EnumerateDirectories(shared).OrderByDescending(d => ParseVersion(Path.GetFileName(d))).FirstOrDefault()
            : null;
    }

    private static Version ParseVersion(string text) =>
        Version.TryParse(text.Split('-')[0], out var version) ? version : new Version(0, 0);
}
