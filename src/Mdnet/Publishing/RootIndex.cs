using System.Text;

namespace Mdnet.Publishing;

/// <summary>
/// <c>index.md</c> at the docs root: the entry point for agents, listing every package folder that has a
/// manifest (generated or downloaded).
/// </summary>
public static class RootIndex
{
    public static void Write(string root)
    {
        if (!Directory.Exists(root))
        {
            return;
        }

        var sb = new StringBuilder("# API documentation\n\n");
        var any = false;
        foreach (var dir in Directory.EnumerateDirectories(root).Order(StringComparer.OrdinalIgnoreCase))
        {
            Manifest? manifest;
            try
            {
                manifest = Manifest.TryLoad(dir);
            }
            catch (Exception e) when (e is InvalidDataException or System.Text.Json.JsonException)
            {
                continue;
            }

            if (manifest is null)
            {
                continue;
            }

            any = true;
            var folder = Path.GetFileName(dir);
            sb.Append("* [").Append(manifest.Id);
            if (manifest.Version is not null)
            {
                sb.Append(' ').Append(manifest.Version);
            }

            sb.Append("](").Append(folder).Append("/index.md)");
            if (Description(Path.Combine(dir, "index.md")) is { } description)
            {
                sb.Append(": ").Append(description);
            }

            sb.Append('\n');
        }

        if (!any)
        {
            sb.Append("No packages.\n");
        }

        File.WriteAllText(Path.Combine(root, "index.md"), sb.ToString());
    }

    /// <summary>The <c>&gt; description</c> line that follows the package heading.</summary>
    private static string? Description(string indexPath)
    {
        if (!File.Exists(indexPath))
        {
            return null;
        }

        foreach (var line in File.ReadLines(indexPath).Take(5))
        {
            if (line.StartsWith("> ", StringComparison.Ordinal))
            {
                return line[2..].Trim();
            }
        }

        return null;
    }
}
