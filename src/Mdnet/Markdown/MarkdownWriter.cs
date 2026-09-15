using System.Text;
using System.Text.RegularExpressions;
using Mdnet.Model;
using Mdnet.Publishing;

namespace Mdnet.Markdown;

/// <summary>
/// Writes the Markdown docs: one folder per package with <c>index.md</c>, one page per type and
/// <c>mdnet.json</c>. The pages are plain Markdown (plus a <c>{% member %}</c> wrapper) so agents read them
/// directly and the Markdoc renderer turns the same files into HTML.
/// </summary>
public sealed partial class MarkdownWriter
{
    private readonly Dictionary<string, (string Page, string? Anchor)> _links = new(StringComparer.Ordinal);

    /// <summary>Writes all packages. Cross-package <c>cref</c>s become relative links.</summary>
    public IReadOnlyDictionary<string, string> Write(IReadOnlyList<DocPackage> packages, string outputRoot)
    {
        foreach (var package in packages)
        {
            foreach (var type in package.Namespaces.SelectMany(n => n.Types))
            {
                var page = $"{package.Id}/{type.Path}";
                _links.TryAdd(type.DocId, (page, null));
                foreach (var member in type.Sections.SelectMany(s => s.Members))
                {
                    _links.TryAdd(member.DocId, (page, member.Anchor));
                }
            }
        }

        var written = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var package in packages)
        {
            var files = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["index.md"] = PackageIndex(package),
            };
            foreach (var type in package.Namespaces.SelectMany(n => n.Types))
            {
                files[type.Path] = TypePage(type, $"{package.Id}/{type.Path}");
            }

            var packageDir = Path.Combine(outputRoot, package.Id);
            DocsFolder.Replace(packageDir, files, new Manifest(package.Id, package.Version));
            written[package.Id] = packageDir;
        }

        RootIndex.Write(outputRoot);
        return written;
    }

    // ---- pages ------------------------------------------------------------------------------

    private string PackageIndex(DocPackage package)
    {
        var sb = new StringBuilder();
        sb.Append("# ").Append(package.Id);
        if (package.Version is not null)
        {
            sb.Append(' ').Append(package.Version);
        }

        sb.Append("\n\n");
        if (!string.IsNullOrWhiteSpace(package.Description))
        {
            sb.Append("> ").Append(Regex.Replace(package.Description.Trim(), @"\s+", " ")).Append("\n\n");
        }

        var page = $"{package.Id}/index.md";
        foreach (var ns in package.Namespaces)
        {
            sb.Append("## ").Append(ns.Name).Append("\n\n");
            foreach (var type in ns.Types)
            {
                sb.Append("* [").Append(type.Name).Append("](").Append(type.Path).Append(')');
                if (FirstSentence(type.Doc.Summary) is { } summary)
                {
                    sb.Append(": ").Append(ResolveLinks(summary, page));
                }

                sb.Append('\n');
            }

            sb.Append('\n');
        }

        return sb.ToString().TrimEnd() + "\n";
    }

    public string TypePage(TypeDoc type, string page)
    {
        var sb = new StringBuilder();
        sb.Append("# ").Append(type.Name).Append("\n\n");
        sb.Append(string.Join(" ", new[] { type.Kind }.Concat(type.Badges).Select(b => $"`{b}`"))).Append("\n\n");
        sb.Append("```csharp\nnamespace ").Append(type.Namespace).Append(";\n\n").Append(type.Declaration).Append("\n```\n\n");
        AppendDoc(sb, type.Doc, page);

        foreach (var section in type.Sections)
        {
            sb.Append("## ").Append(section.Title).Append("\n\n");
            if (section.NestedTypes is { } nested)
            {
                foreach (var nestedType in nested)
                {
                    sb.Append("* [").Append(nestedType.Name).Append("](").Append(Relative(page, $"{page[..page.IndexOf('/')]}/{nestedType.Path}")).Append(')');
                    if (FirstSentence(nestedType.Doc.Summary) is { } summary)
                    {
                        sb.Append(": ").Append(ResolveLinks(summary, page));
                    }

                    sb.Append('\n');
                }

                sb.Append('\n');
                continue;
            }

            foreach (var member in section.Members)
            {
                sb.Append("{% member id=\"").Append(member.Anchor).Append("\" %}\n");
                sb.Append("```csharp\n").Append(member.Signature).Append("\n```\n\n");
                AppendDoc(sb, member.Doc, page);
                sb.Append("{% /member %}\n\n");
            }
        }

        return Regex.Replace(sb.ToString(), @"\n{3,}", "\n\n").TrimEnd() + "\n";
    }

    private void AppendDoc(StringBuilder sb, DocComment doc, string page)
    {
        void Block(string? markdown)
        {
            if (!string.IsNullOrWhiteSpace(markdown))
            {
                sb.Append(ResolveLinks(markdown, page)).Append("\n\n");
            }
        }

        void Labeled(string label, string? markdown)
        {
            if (!string.IsNullOrWhiteSpace(markdown))
            {
                sb.Append("**").Append(label).Append("**: ").Append(ResolveLinks(markdown, page)).Append("\n\n");
            }
        }

        Block(doc.Summary);

        var parameters = doc.TypeParams.Concat(doc.Params).ToList();
        if (parameters.Count > 0)
        {
            foreach (var p in parameters)
            {
                sb.Append("* **").Append(p.Name).Append("**: ").Append(ResolveLinks(p.Text, page)).Append('\n');
            }

            sb.Append('\n');
        }

        Labeled("Value", doc.Value);
        Labeled("Returns", doc.Returns);
        foreach (var exception in doc.Exceptions)
        {
            sb.Append("**Throws** ").Append(ResolveLinks(exception.Name, page));
            if (exception.Text.Length > 0)
            {
                sb.Append(": ").Append(ResolveLinks(exception.Text, page));
            }

            sb.Append("\n\n");
        }

        if (!string.IsNullOrWhiteSpace(doc.Remarks))
        {
            var lines = ResolveLinks(doc.Remarks, page).Split('\n');
            lines[0] = "**Remarks**: " + lines[0];
            sb.Append(string.Join("\n", lines.Select(l => l.Length == 0 ? ">" : "> " + l))).Append("\n\n");
        }

        foreach (var example in doc.Examples)
        {
            Block(example);
        }

        if (doc.SeeAlso.Count > 0)
        {
            Labeled("See also", string.Join(", ", doc.SeeAlso));
        }
    }

    // ---- links ------------------------------------------------------------------------------

    private string ResolveLinks(string markdown, string page) =>
        XrefLink().Replace(
            markdown,
            match =>
            {
                var label = match.Groups["label"].Value;
                var id = Uri.UnescapeDataString(match.Groups["id"].Value);
                if (!_links.TryGetValue(id, out var target))
                {
                    return label;
                }

                if (target.Page == page && target.Anchor is null)
                {
                    return label;
                }

                var href = target.Page == page ? "" : Relative(page, target.Page);
                if (target.Anchor is not null)
                {
                    href += "#" + target.Anchor;
                }

                return $"[{label}]({href})";
            }
        );

    /// <summary>Relative URL from one page to another, both relative to the docs root.</summary>
    public static string Relative(string fromPage, string toPage)
    {
        var from = fromPage.Split('/')[..^1];
        var to = toPage.Split('/');
        var common = 0;
        while (common < from.Length && common < to.Length - 1 && from[common] == to[common])
        {
            common++;
        }

        return string.Join("/", Enumerable.Repeat("..", from.Length - common).Concat(to[common..]));
    }

    private static string? FirstSentence(string? summary)
    {
        if (string.IsNullOrWhiteSpace(summary))
        {
            return null;
        }

        var paragraph = summary.Split("\n\n")[0];
        if (paragraph.StartsWith("```", StringComparison.Ordinal))
        {
            return null;
        }

        var end = SentenceEnd().Match(paragraph);
        return end.Success ? paragraph[..(end.Index + 1)] : paragraph;
    }

    [GeneratedRegex(@"\[(?<label>(?:[^\[\]]|`[^`]*`)*)\]\(xref:(?<id>[^)\s]+)\)")]
    private static partial Regex XrefLink();

    [GeneratedRegex(@"(?<!\b(?:e\.g|i\.e|etc|vs))\.(\s+(?=[A-Z`\[*])|$)")]
    private static partial Regex SentenceEnd();
}
