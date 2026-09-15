using System.Xml.Linq;
using Mdnet.Extraction;
using Mdnet.Model;
using Mdnet.Signatures;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Mdnet.Loading;

/// <summary>A package reference matched by pattern, with the restore context it came from.</summary>
public sealed record MatchedPackage(RestoredPackage Package, RestoredTarget Target);

/// <summary>Package mode: documents NuGet packages referenced by projects, straight from the global packages folder.</summary>
public sealed class PackageLoader(Log log)
{
    /// <summary>Packages matching <paramref name="patterns"/> across all projects under <paramref name="path"/>; highest version wins.</summary>
    public static async Task<IReadOnlyList<MatchedPackage>> FindAsync(string path, Glob patterns, Log log, CancellationToken cancellationToken)
    {
        var matches = new Dictionary<string, MatchedPackage>(StringComparer.OrdinalIgnoreCase);
        foreach (var project in ProjectDiscovery.FindProjects(path).Where(File.Exists))
        {
            var target = await AssetsFile.LoadAsync(project, log, cancellationToken);
            if (target is null)
            {
                continue;
            }

            foreach (var package in target.Packages.Where(p => patterns.IsMatch(p.Id)))
            {
                if (
                    !matches.TryGetValue(package.Id, out var existing)
                    || PackageVersionComparer.Instance.Compare(package.Version, existing.Package.Version) > 0
                )
                {
                    matches[package.Id] = new MatchedPackage(package, target);
                }
            }
        }

        return matches.Values.OrderBy(m => m.Package.Id, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public async Task<IReadOnlyList<DocPackage>> LoadAsync(string path, Glob patterns, Visibility visibility, CancellationToken cancellationToken)
    {
        var matches = await FindAsync(path, patterns, log, cancellationToken);
        if (matches.Count == 0)
        {
            log.Warn($"No package references under {path} match the pattern.");
            return [];
        }

        var frameworkCache = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        var result = new List<DocPackage>();
        foreach (var (package, target) in matches)
        {
            if (package.Directory is null)
            {
                log.Warn($"{package.Id} {package.Version}: not found in the NuGet packages folder (run dotnet restore).");
                continue;
            }

            var documented = package.CompileAssets.Count > 0 ? package.CompileAssets : package.RuntimeAssets;
            if (documented.Count == 0)
            {
                log.Verbose($"Skipping {package.Id}: no assemblies.");
                continue;
            }

            log.Info($"Documenting {package.Id} {package.Version}");
            var frameworkKey = target.TargetFramework + "|" + string.Join(",", target.FrameworkReferences);
            if (!frameworkCache.TryGetValue(frameworkKey, out var frameworkAssemblies))
            {
                frameworkCache[frameworkKey] = frameworkAssemblies = FrameworkReferences.Find(target.TargetFramework, target.FrameworkReferences, log);
            }

            var documentedReferences = documented.Select(dll => CreateReference(dll, package)).ToList();
            var references = new Dictionary<string, MetadataReference>(StringComparer.OrdinalIgnoreCase);
            foreach (var dll in frameworkAssemblies)
            {
                references[Path.GetFileName(dll)] = CreateReference(dll, null);
            }

            foreach (var other in target.Packages.Where(p => p != package))
            {
                foreach (var dll in other.CompileAssets)
                {
                    references.TryAdd(Path.GetFileName(dll), CreateReference(dll, other));
                }
            }

            for (var i = 0; i < documented.Count; i++)
            {
                references[Path.GetFileName(documented[i])] = documentedReferences[i];
            }

            var compilation = CSharpCompilation.Create(
                "mdnet.docs",
                references: references.Values,
                options: new CSharpCompilationOptions(
                    OutputKind.DynamicallyLinkedLibrary,
                    nullableContextOptions: NullableContextOptions.Enable,
                    metadataImportOptions: visibility.Level == VisibilityLevel.Internal ? MetadataImportOptions.All : MetadataImportOptions.Public
                )
            );

            var extractor = new ApiExtractor(compilation, visibility);
            var parts = documentedReferences
                .Select(r => compilation.GetAssemblyOrModuleSymbol(r) as IAssemblySymbol)
                .OfType<IAssemblySymbol>()
                .Select(assembly => extractor.Extract(assembly, package.Id, package.Version, null))
                .ToList();
            var namespaces = parts.SelectMany(p => p.Namespaces)
                .GroupBy(n => n.Name, StringComparer.Ordinal)
                .OrderBy(g => g.Key, StringComparer.Ordinal)
                .Select(g => new NamespaceDoc(g.Key, g.SelectMany(n => n.Types).OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase).ToList()))
                .ToList();
            if (namespaces.Count == 0)
            {
                log.Verbose($"Skipping {package.Id}: no documented types.");
                continue;
            }

            result.Add(new DocPackage(package.Id, package.Version, NuspecDescription(package), namespaces));
        }

        return result;
    }

    private static MetadataReference CreateReference(string dll, RestoredPackage? package)
    {
        var xml = FindXmlDoc(dll, package);
        return MetadataReference.CreateFromFile(dll, documentation: xml is null ? null : XmlDocumentationProvider.CreateFromFile(xml));
    }

    /// <summary>XML docs next to the assembly, else next to the same-named assembly elsewhere in the package.</summary>
    private static string? FindXmlDoc(string dll, RestoredPackage? package)
    {
        var sibling = Path.ChangeExtension(dll, ".xml");
        if (File.Exists(sibling))
        {
            return sibling;
        }

        if (package?.Directory is null)
        {
            return null;
        }

        var name = Path.GetFileNameWithoutExtension(dll) + ".xml";
        return package.RuntimeAssets.Select(r => Path.Combine(Path.GetDirectoryName(r)!, name)).FirstOrDefault(File.Exists)
            ?? Directory.EnumerateFiles(package.Directory, name, SearchOption.AllDirectories).FirstOrDefault();
    }

    private static string? NuspecDescription(RestoredPackage package)
    {
        var nuspec = package.Directory is null ? null : Directory.EnumerateFiles(package.Directory, "*.nuspec").FirstOrDefault();
        if (nuspec is null)
        {
            return null;
        }

        try
        {
            return XDocument.Load(nuspec).Descendants().FirstOrDefault(e => e.Name.LocalName == "description")?.Value.Trim();
        }
        catch (System.Xml.XmlException)
        {
            return null;
        }
    }
}
