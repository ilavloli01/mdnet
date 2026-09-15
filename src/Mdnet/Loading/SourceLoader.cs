using System.Text.RegularExpressions;
using System.Xml.Linq;
using Mdnet.Extraction;
using Mdnet.Model;
using Mdnet.Signatures;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;

namespace Mdnet.Loading;

/// <summary>Source mode: loads projects with MSBuildWorkspace and documents their compilations.</summary>
public sealed partial class SourceLoader(Log log)
{
    private static readonly string[] TestPackages = ["Microsoft.NET.Test.Sdk", "TUnit", "xunit", "xunit.v3", "NUnit", "MSTest", "MSTest.TestFramework"];

    public async Task<IReadOnlyList<DocPackage>> LoadAsync(
        string path,
        Glob include,
        Glob exclude,
        Visibility visibility,
        CancellationToken cancellationToken
    )
    {
        var projectFiles = ProjectDiscovery.FindProjects(path)
            .Where(p => File.Exists(p))
            .Where(p => include.IsEmpty || include.IsMatch(Path.GetFileNameWithoutExtension(p)))
            .Where(p => !exclude.IsMatch(Path.GetFileNameWithoutExtension(p)))
            .Where(p => !IsTestProject(p))
            .ToList();
        if (projectFiles.Count == 0)
        {
            log.Warn($"No projects to document under {path}.");
            return [];
        }

        using var workspace = MSBuildWorkspace.Create();
        workspace.RegisterWorkspaceFailedHandler(e => log.Verbose($"workspace: {e.Diagnostic.Message}"));

        foreach (var file in projectFiles)
        {
            if (workspace.CurrentSolution.Projects.Any(p => SamePath(p.FilePath, file)))
            {
                continue;
            }

            log.Info($"Loading {Path.GetFileName(file)}");
            await workspace.OpenProjectAsync(file, cancellationToken: cancellationToken);
        }

        var packages = new List<DocPackage>();
        foreach (var file in projectFiles)
        {
            var project = workspace.CurrentSolution.Projects
                .Where(p => SamePath(p.FilePath, file) && p.Language == LanguageNames.CSharp)
                .OrderByDescending(p => TargetFrameworkRank(p.Name))
                .FirstOrDefault();
            if (project is null)
            {
                log.Warn($"Skipping {Path.GetFileName(file)}: not a loadable C# project.");
                continue;
            }

            var compilation = await project.GetCompilationAsync(cancellationToken);
            if (compilation is null)
            {
                continue;
            }

            var errors = compilation.GetDiagnostics(cancellationToken).Count(d => d.Severity == DiagnosticSeverity.Error);
            if (errors > 0)
            {
                log.Warn($"{project.Name}: {errors} compilation error(s); signatures may be incomplete (run dotnet restore/build first).");
            }

            var info = ProjectInfoFromFile(file, compilation.Assembly);
            var package = new ApiExtractor(compilation, visibility).Extract(compilation.Assembly, info.Id, info.Version, info.Description);
            if (package.Namespaces.Count == 0)
            {
                log.Verbose($"Skipping {info.Id}: no documented types.");
                continue;
            }

            packages.Add(package);
        }

        return packages;
    }

    private static bool SamePath(string? a, string b) =>
        a is not null && string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);

    /// <summary><c>Lib(net10.0)</c> ranks above <c>Lib(netstandard2.0)</c>.</summary>
    private static double TargetFrameworkRank(string projectName)
    {
        var match = TfmVersion().Match(projectName);
        if (!match.Success)
        {
            return 0;
        }

        var version = double.Parse(match.Groups["v"].Value, System.Globalization.CultureInfo.InvariantCulture);
        return match.Groups["std"].Success ? version / 10 : version;
    }

    private static bool IsTestProject(string file)
    {
        try
        {
            var doc = XDocument.Load(file);
            var properties = doc.Descendants().Where(e => e.Parent?.Name.LocalName == "PropertyGroup");
            if (properties.Any(e => e.Name.LocalName == "IsTestProject" && e.Value.Trim().Equals("true", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            return doc.Descendants()
                .Where(e => e.Name.LocalName == "PackageReference")
                .Select(e => e.Attribute("Include")?.Value)
                .Any(name => name is not null && TestPackages.Contains(name, StringComparer.OrdinalIgnoreCase));
        }
        catch (System.Xml.XmlException)
        {
            return false;
        }
    }

    private static (string Id, string? Version, string? Description) ProjectInfoFromFile(string file, IAssemblySymbol assembly)
    {
        string? Property(XDocument doc, string name) =>
            doc.Descendants().LastOrDefault(e => e.Name.LocalName == name && e.Parent?.Name.LocalName == "PropertyGroup")?.Value.Trim() is { Length: > 0 } v
            && !v.Contains("$(", StringComparison.Ordinal)
                ? v
                : null;

        var doc = XDocument.Load(file);
        var id = Property(doc, "PackageId") ?? assembly.Name;
        var version = Property(doc, "PackageVersion") ?? Property(doc, "Version") ?? Property(doc, "VersionPrefix") ?? FormatVersion(assembly.Identity.Version);
        return (id, version, Property(doc, "Description"));
    }

    private static string? FormatVersion(Version version) =>
        version == new Version(0, 0, 0, 0) || version == new Version(1, 0, 0, 0)
            ? null
            : version.Revision == 0 ? version.ToString(3) : version.ToString();

    [GeneratedRegex(@"\((?:net(?<std>standard)?|netcoreapp)(?<v>\d+(?:\.\d+)?)")]
    private static partial Regex TfmVersion();
}
