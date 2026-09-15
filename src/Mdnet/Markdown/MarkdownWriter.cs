using System.Text;
using System.Text.RegularExpressions;
using Mdnet.Loading;
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
        var meta = package.Metadata;
        var description = MsBuildProperties.Normalize(meta.Description);
        // Lists need a brief; without a <Description> the README's first paragraph stands in (the page shows the README itself).
        var brief = description ?? (package.Readme is null ? null : ReadmeImporter.FirstParagraph(ReadmeImporter.Import(package.Readme, 3, package.Id) ?? ""));
        var sb = new StringBuilder();
        sb.Append(
            Frontmatter.Write(
                [
                    new(PackageFields.Version, package.Version),
                    new(PackageFields.Title, meta.Title),
                    new(PackageFields.Description, brief),
                    new(PackageFields.Authors, meta.Authors),
                    new(PackageFields.Company, meta.Company),
                    new(PackageFields.Copyright, meta.Copyright),
                    new(PackageFields.Tags, meta.Tags),
                    new(PackageFields.License, meta.License),
                    new(PackageFields.Project, meta.ProjectUrl),
                    new(PackageFields.Repository, meta.RepositoryUrl),
                    new(PackageFields.Frameworks, meta.Frameworks is { Count: > 0 } f ? string.Join(", ", f) : null),
                    new(PackageFields.Types, package.Namespaces.Sum(n => n.Types.Count).ToString(System.Globalization.CultureInfo.InvariantCulture)),
                ]
            )
        );
        sb.Append("# ").Append(package.Id).Append("\n\n`package`");
        if (package.Version is not null)
        {
            sb.Append(" `").Append(package.Version).Append('`');
        }

        sb.Append("\n\n");
        if (description is not null)
        {
            sb.Append(description).Append("\n\n");
        }

        AppendReadme(sb, package.Readme, 3, package.Id, package.RootNamespace ?? package.Id);

        var page = $"{package.Id}/index.md";
        var namespaces = package.Namespaces.ToDictionary(n => n.Name, StringComparer.Ordinal);
        foreach (var feature in NamespaceTree.Build(package.Namespaces.Select(n => n.Name).ToList(), package.RootNamespace))
        {
            sb.Append("## ").Append(feature.Title).Append("\n\n");
            AppendReadme(sb, package.NamespaceReadmes.GetValueOrDefault(feature.Name), 4, feature.Name, feature.Title);
            foreach (var name in feature.Namespaces)
            {
                if (name != feature.Name)
                {
                    sb.Append("### ").Append(name).Append("\n\n");
                    AppendReadme(sb, package.NamespaceReadmes.GetValueOrDefault(name), 4, name, name[(name.LastIndexOf('.') + 1)..]);
                }

                foreach (var type in namespaces[name].Types)
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
        }

        return Regex.Replace(sb.ToString(), @"\n{3,}", "\n\n").TrimEnd() + "\n";
    }

    /// <summary>README content inside a <c>{% readme %}</c> tag, so its headings and lists stay out of the page structure.</summary>
    private static void AppendReadme(StringBuilder sb, string? readme, int minHeadingLevel, params string[] titles)
    {
        if (readme is not null && ReadmeImporter.Import(readme, minHeadingLevel, titles) is { } content)
        {
            sb.Append("{% readme %}\n").Append(content).Append("\n{% /readme %}\n\n");
        }
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
