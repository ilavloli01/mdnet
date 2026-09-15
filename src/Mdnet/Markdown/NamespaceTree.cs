namespace Mdnet.Markdown;

/// <summary>A top-level feature of a package: a root namespace (below the package's base namespace) and everything nested in it.</summary>
/// <param name="Name">Full namespace of the feature, e.g. <c>Contoso.Domain</c>.</param>
/// <param name="Title">Heading text: the part below the base namespace (<c>Domain</c>), or the full name for the base itself.</param>
/// <param name="Namespaces">Namespaces with types that belong to the feature, the feature's own namespace first when it has types.</param>
public sealed record Feature(string Name, string Title, IReadOnlyList<string> Namespaces);

public static class NamespaceTree
{
    /// <summary>
    /// Groups namespaces by the segment that follows the base namespace. The base is <paramref name="rootNamespace"/>
    /// when the namespaces use it, otherwise their longest common dotted prefix.
    /// </summary>
    public static IReadOnlyList<Feature> Build(IReadOnlyList<string> namespaces, string? rootNamespace)
    {
        var baseName = BaseNamespace(namespaces, rootNamespace);
        return namespaces
            .GroupBy(ns => FeatureOf(ns, baseName), StringComparer.Ordinal)
            .Select(g => new Feature(
                g.Key,
                baseName is null || !g.Key.StartsWith(baseName + ".", StringComparison.Ordinal) ? g.Key : g.Key[(baseName.Length + 1)..],
                g.OrderBy(ns => ns == g.Key ? 0 : 1).ThenBy(ns => ns, StringComparer.Ordinal).ToList()
            ))
            .OrderBy(f => f.Name == baseName ? 0 : baseName is not null && IsWithin(f.Name, baseName) ? 1 : 2)
            .ThenBy(f => f.Name, StringComparer.Ordinal)
            .ToList();
    }

    public static string? BaseNamespace(IReadOnlyList<string> namespaces, string? rootNamespace)
    {
        if (namespaces.Count == 0)
        {
            return null;
        }

        if (rootNamespace is { Length: > 0 } && namespaces.Any(ns => IsWithin(ns, rootNamespace)))
        {
            return rootNamespace;
        }

        var common = namespaces[0].Split('.');
        var length = common.Length;
        foreach (var ns in namespaces.Skip(1))
        {
            var parts = ns.Split('.');
            var i = 0;
            while (i < length && i < parts.Length && parts[i] == common[i])
            {
                i++;
            }

            length = i;
        }

        return length == 0 ? null : string.Join('.', common[..length]);
    }

    /// <summary><c>ns</c> equals <c>parent</c> or is nested in it.</summary>
    public static bool IsWithin(string ns, string parent) =>
        ns == parent || ns.StartsWith(parent + ".", StringComparison.Ordinal);

    private static string FeatureOf(string ns, string? baseName)
    {
        if (baseName is null)
        {
            return ns.Split('.')[0];
        }

        // The base itself, and namespaces outside it (e.g. extension methods in Microsoft.Extensions.*), stand alone.
        if (!IsWithin(ns, baseName) || ns == baseName)
        {
            return ns;
        }

        var next = ns[(baseName.Length + 1)..].Split('.')[0];
        return $"{baseName}.{next}";
    }
}
