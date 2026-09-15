using System.Text.Json;

namespace Mdnet.Loading;

/// <summary>A package resolved by NuGet restore for one target framework of a project.</summary>
/// <param name="Directory">Package folder in the NuGet global packages folder, if present on disk.</param>
/// <param name="CompileAssets">Absolute paths of reference assemblies (placeholders removed).</param>
/// <param name="RuntimeAssets">Absolute paths of implementation assemblies (placeholders removed).</param>
public sealed record RestoredPackage(
    string Id,
    string Version,
    string? Directory,
    IReadOnlyList<string> CompileAssets,
    IReadOnlyList<string> RuntimeAssets
);

/// <summary>One target of <c>project.assets.json</c>: all restored packages plus framework references.</summary>
public sealed record RestoredTarget(
    string ProjectPath,
    string TargetFramework,
    IReadOnlyList<RestoredPackage> Packages,
    IReadOnlyList<string> FrameworkReferences
);

/// <summary>Reads <c>obj/project.assets.json</c>, restoring the project first when it is missing.</summary>
public static class AssetsFile
{
    public static async Task<RestoredTarget?> LoadAsync(string projectPath, Log log, CancellationToken cancellationToken)
    {
        var assetsPath = await LocateAsync(projectPath, log, cancellationToken);
        if (assetsPath is null)
        {
            log.Warn($"{Path.GetFileName(projectPath)}: no project.assets.json (restore failed?).");
            return null;
        }

        using var stream = File.OpenRead(assetsPath);
        using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        return Parse(projectPath, json.RootElement);
    }

    public static RestoredTarget? Parse(string projectPath, JsonElement root)
    {
        var packageFolders = root.TryGetProperty("packageFolders", out var folders)
            ? folders.EnumerateObject().Select(f => f.Name).ToList()
            : [];

        // Pick the highest framework target without a runtime identifier.
        var target = root.GetProperty("targets")
            .EnumerateObject()
            .Where(t => !t.Name.Contains('/', StringComparison.Ordinal))
            .OrderByDescending(t => FrameworkRank(t.Name))
            .Select(t => (JsonProperty?)t)
            .FirstOrDefault();
        if (target is null)
        {
            return null;
        }

        var libraries = root.GetProperty("libraries");
        var packages = new List<RestoredPackage>();
        foreach (var entry in target.Value.Value.EnumerateObject())
        {
            if (!entry.Value.TryGetProperty("type", out var type) || type.GetString() != "package")
            {
                continue;
            }

            var slash = entry.Name.IndexOf('/', StringComparison.Ordinal);
            var id = entry.Name[..slash];
            var version = entry.Name[(slash + 1)..];
            var libraryPath = libraries.TryGetProperty(entry.Name, out var library) && library.TryGetProperty("path", out var p)
                ? p.GetString()
                : $"{id.ToLowerInvariant()}/{version.ToLowerInvariant()}";
            var directory = packageFolders
                .Select(folder => Path.Combine(folder, libraryPath!.Replace('/', Path.DirectorySeparatorChar)))
                .FirstOrDefault(System.IO.Directory.Exists);

            packages.Add(
                new RestoredPackage(id, version, directory, Assets(entry.Value, "compile", directory), Assets(entry.Value, "runtime", directory))
            );
        }

        var frameworkReferences = new List<string> { "Microsoft.NETCore.App" };
        if (
            root.TryGetProperty("project", out var project)
            && project.TryGetProperty("frameworks", out var frameworks)
        )
        {
            foreach (var framework in frameworks.EnumerateObject())
            {
                if (framework.Value.TryGetProperty("frameworkReferences", out var refs))
                {
                    frameworkReferences.AddRange(refs.EnumerateObject().Select(r => r.Name));
                }
            }
        }

        return new RestoredTarget(projectPath, TargetFrameworkMoniker(target.Value.Name), packages, frameworkReferences.Distinct(StringComparer.OrdinalIgnoreCase).ToList());
    }

    private static IReadOnlyList<string> Assets(JsonElement library, string kind, string? directory)
    {
        if (directory is null || !library.TryGetProperty(kind, out var assets))
        {
            return [];
        }

        return assets.EnumerateObject()
            .Select(a => a.Name)
            .Where(a => a.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            .Select(a => Path.Combine(directory, a.Replace('/', Path.DirectorySeparatorChar)))
            .Where(File.Exists)
            .ToList();
    }

    private static async Task<string?> LocateAsync(string projectPath, Log log, CancellationToken cancellationToken)
    {
        var conventional = Path.Combine(Path.GetDirectoryName(projectPath)!, "obj", "project.assets.json");
        if (File.Exists(conventional))
        {
            return conventional;
        }

        var property = await ProcessRunner.RunAsync("dotnet", ["msbuild", projectPath, "-getProperty:ProjectAssetsFile"], cancellationToken: cancellationToken);
        var custom = property.Success ? property.Output.Trim() : "";
        if (custom.Length > 0 && File.Exists(custom))
        {
            return custom;
        }

        log.Info($"Restoring {Path.GetFileName(projectPath)}");
        var restore = await ProcessRunner.RunAsync("dotnet", ["restore", projectPath], cancellationToken: cancellationToken);
        if (!restore.Success)
        {
            log.Verbose(restore.Output + restore.Error);
        }

        return File.Exists(conventional) ? conventional : custom.Length > 0 && File.Exists(custom) ? custom : null;
    }

    /// <summary><c>net10.0</c> from assets target names such as <c>net10.0</c> or <c>.NETCoreApp,Version=v10.0</c>.</summary>
    public static string TargetFrameworkMoniker(string target)
    {
        if (!target.Contains(",Version=v", StringComparison.Ordinal))
        {
            return target;
        }

        var name = target[..target.IndexOf(',', StringComparison.Ordinal)];
        var version = target[(target.IndexOf("=v", StringComparison.Ordinal) + 2)..];
        return name switch
        {
            ".NETCoreApp" when Version.Parse(version).Major >= 5 => "net" + version,
            ".NETCoreApp" => "netcoreapp" + version,
            ".NETStandard" => "netstandard" + version,
            ".NETFramework" => "net" + version.Replace(".", "", StringComparison.Ordinal),
            _ => target,
        };
    }

    public static double FrameworkRank(string target)
    {
        var tfm = TargetFrameworkMoniker(target);
        if (tfm.StartsWith("netstandard", StringComparison.Ordinal))
        {
            return double.TryParse(tfm["netstandard".Length..], System.Globalization.CultureInfo.InvariantCulture, out var v) ? v / 10 : 0;
        }

        if (tfm.StartsWith("netcoreapp", StringComparison.Ordinal))
        {
            return double.TryParse(tfm["netcoreapp".Length..], System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 0;
        }

        return tfm.StartsWith("net", StringComparison.Ordinal)
            && tfm.Contains('.', StringComparison.Ordinal)
            && double.TryParse(tfm[3..].Split('-')[0], System.Globalization.CultureInfo.InvariantCulture, out var n)
            ? n
            : 0.01;
    }
}
