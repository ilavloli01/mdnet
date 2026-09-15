using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Mdnet.Loading;

/// <summary>Resolves a solution (.sln/.slnx), project (.csproj) or directory into project files.</summary>
public static partial class ProjectDiscovery
{
    private static readonly string[] IgnoredDirectories = ["bin", "obj", "node_modules", ".git", ".vs", ".idea"];

    public static IReadOnlyList<string> FindProjects(string path)
    {
        var full = Path.GetFullPath(path);
        if (Directory.Exists(full))
        {
            return EnumerateProjects(full).Order(StringComparer.OrdinalIgnoreCase).ToList();
        }

        if (!File.Exists(full))
        {
            throw new FileNotFoundException($"Path not found: {path}");
        }

        var directory = Path.GetDirectoryName(full)!;
        return Path.GetExtension(full).ToLowerInvariant() switch
        {
            ".csproj" => [full],
            ".slnx" => XDocument.Load(full)
                .Descendants("Project")
                .Select(p => p.Attribute("Path")?.Value)
                .OfType<string>()
                .Where(p => p.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
                .Select(p => Path.GetFullPath(Path.Combine(directory, p.Replace('\\', Path.DirectorySeparatorChar))))
                .ToList(),
            ".sln" => SlnProject()
                .Matches(File.ReadAllText(full))
                .Select(m => Path.GetFullPath(Path.Combine(directory, m.Groups["path"].Value.Replace('\\', Path.DirectorySeparatorChar))))
                .ToList(),
            _ => throw new ArgumentException($"Expected a .sln, .slnx, .csproj or directory: {path}"),
        };
    }

    /// <summary>The solution file for source mode: the path itself, or the only solution in a directory.</summary>
    public static string? FindSolution(string path)
    {
        var full = Path.GetFullPath(path);
        if (File.Exists(full))
        {
            return Path.GetExtension(full) is ".sln" or ".slnx" ? full : null;
        }

        var solutions = Directory.GetFiles(full, "*.slnx").Concat(Directory.GetFiles(full, "*.sln")).ToList();
        return solutions.Count == 1 ? solutions[0] : null;
    }

    private static IEnumerable<string> EnumerateProjects(string directory)
    {
        foreach (var file in Directory.EnumerateFiles(directory, "*.csproj"))
        {
            yield return file;
        }

        foreach (var child in Directory.EnumerateDirectories(directory))
        {
            if (IgnoredDirectories.Contains(Path.GetFileName(child), StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (var file in EnumerateProjects(child))
            {
                yield return file;
            }
        }
    }

    [GeneratedRegex(@"^Project\(""\{[^}]+\}""\)\s*=\s*""[^""]*"",\s*""(?<path>[^""]+\.csproj)""", RegexOptions.Multiline)]
    private static partial Regex SlnProject();
}
