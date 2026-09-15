using System.Globalization;
using System.Text;

namespace Mdnet.Publishing;

/// <summary>
/// <c>index.md</c> at the docs root: the entry point for agents, listing every package folder that has a
/// manifest (generated or downloaded), grouped by the first segment of the package id.
/// </summary>
public static class RootIndex
{
    private sealed record Entry(string Folder, Manifest Manifest, IReadOnlyDictionary<string, string> Fields)
    {
        public string Group => Manifest.Id.Split('.')[0];

        public int Types => int.TryParse(Fields.GetValueOrDefault(PackageFields.Types), CultureInfo.InvariantCulture, out var n) ? n : 0;
    }

    public static void Write(string root)
    {
        if (!Directory.Exists(root))
        {
            return;
        }

        var entries = new List<Entry>();
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

            if (manifest is not null)
            {
                var index = Path.Combine(dir, "index.md");
                var fields = File.Exists(index) ? Frontmatter.Parse(File.ReadAllText(index)) : new Dictionary<string, string>();
                entries.Add(new Entry(Path.GetFileName(dir), manifest, fields));
            }
        }

        var sb = new StringBuilder("# API documentation\n\n");
        if (entries.Count == 0)
        {
            sb.Append("No packages.\n");
        }
        else
        {
            var types = entries.Sum(e => e.Types);
            sb.Append(Count(entries.Count, "package"));
            if (types > 0)
            {
                sb.Append(" · ").Append(Count(types, "type"));
            }

            sb.Append("\n\n");
            var groups = entries.GroupBy(e => e.Group, StringComparer.OrdinalIgnoreCase).ToList();
            foreach (var group in groups)
            {
                if (groups.Count > 1)
                {
                    sb.Append("## ").Append(group.Key).Append("\n\n");
                }

                foreach (var entry in group)
                {
                    sb.Append("* [").Append(entry.Manifest.Id).Append("](").Append(entry.Folder).Append("/index.md)");
                    if ((entry.Manifest.Version ?? entry.Fields.GetValueOrDefault(PackageFields.Version)) is { } version)
                    {
                        sb.Append(" `").Append(version).Append('`');
                    }

                    if (entry.Fields.GetValueOrDefault(PackageFields.Description) is { Length: > 0 } description)
                    {
                        sb.Append(": ").Append(description);
                    }

                    sb.Append('\n');
                }

                sb.Append('\n');
            }
        }

        File.WriteAllText(Path.Combine(root, "index.md"), sb.ToString().TrimEnd() + "\n");
    }

    private static string Count(int n, string noun) => $"{n.ToString(CultureInfo.InvariantCulture)} {noun}{(n == 1 ? "" : "s")}";
}
