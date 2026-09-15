using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using Mdnet.Model;
using Microsoft.CodeAnalysis;

namespace Mdnet.Docs;

/// <summary>Turns XML doc comments into Markdown fragments, resolving <c>&lt;inheritdoc/&gt;</c>.</summary>
public sealed partial class XmlDocParser(Compilation compilation)
{
    private static readonly string[] SingleTags = ["summary", "value", "returns", "remarks"];

    public DocComment Parse(ISymbol symbol)
    {
        var xml = Resolve(symbol, depth: 0);
        if (xml is null)
        {
            return DocComment.Empty;
        }

        return new DocComment(
            Summary: Block(xml.Element("summary")),
            TypeParams: Named(xml, "typeparam"),
            Params: Named(xml, "param"),
            Value: Block(xml.Element("value")),
            Returns: Block(xml.Element("returns")),
            Exceptions: xml.Elements("exception")
                .Select(e => new DocParam(CrefLink(e.Attribute("cref")?.Value), Inline(e)))
                .Where(e => e.Name.Length > 0)
                .ToList(),
            Remarks: Block(xml.Element("remarks")),
            Examples: xml.Elements("example").Select(Block).OfType<string>().ToList(),
            SeeAlso: xml.Elements("seealso").Select(e => LinkElement(e)).Where(s => s.Length > 0).ToList()
        );
    }

    private IReadOnlyList<DocParam> Named(XElement xml, string tag) =>
        xml.Elements(tag)
            .Select(e => new DocParam(e.Attribute("name")?.Value ?? "", Inline(e)))
            // Empty <param/> tags add nothing the signature does not already show.
            .Where(p => p.Name.Length > 0 && p.Text.Length > 0)
            .ToList();

    // ---- inheritdoc -------------------------------------------------------------------------

    private XElement? Resolve(ISymbol symbol, int depth)
    {
        if (depth > 8)
        {
            return null;
        }

        var own = Load(symbol);
        var inheritdoc = own?.Element("inheritdoc");
        var hasOwnContent = own is not null && own.Elements().Any(e => e.Name != "inheritdoc");
        if (own is not null && inheritdoc is null && hasOwnContent)
        {
            return own;
        }

        // Explicit <inheritdoc/>, or no docs at all on an override/implementation: inherit.
        var cref = inheritdoc?.Attribute("cref")?.Value;
        var source = cref is not null
            ? DocumentationCommentId.GetFirstSymbolForDeclarationId(cref, compilation)
                ?? DocumentationCommentId.GetFirstSymbolForReferenceId(cref, compilation)
            : InheritanceSource(symbol);
        var inherited = source is null ? null : Resolve(source, depth + 1);
        if (inherited is null)
        {
            return hasOwnContent ? own : null;
        }

        if (own is null)
        {
            return inherited;
        }

        var merged = new XElement("member", own.Elements().Where(e => e.Name != "inheritdoc"));
        foreach (var element in inherited.Elements())
        {
            var name = element.Name.LocalName;
            var exists = SingleTags.Contains(name) || name == "example"
                ? merged.Element(name) is not null
                : merged.Elements(name).Any(e => Key(e) == Key(element));
            if (!exists)
            {
                merged.Add(element);
            }
        }

        return merged;

        static string? Key(XElement e) => e.Attribute("name")?.Value ?? e.Attribute("cref")?.Value;
    }

    private static ISymbol? InheritanceSource(ISymbol symbol)
    {
        switch (symbol)
        {
            case INamedTypeSymbol type:
                return type.BaseType is { SpecialType: not SpecialType.System_Object and not SpecialType.System_ValueType }
                    ? type.BaseType.OriginalDefinition
                    : type.Interfaces.FirstOrDefault()?.OriginalDefinition;
            case IMethodSymbol { MethodKind: MethodKind.Constructor } ctor:
                return ctor
                    .ContainingType.BaseType?.InstanceConstructors.FirstOrDefault(c =>
                        c.Parameters.Select(p => p.Type)
                            .SequenceEqual(ctor.Parameters.Select(p => p.Type), SymbolEqualityComparer.Default)
                    )
                    ?.OriginalDefinition;
            case IMethodSymbol { OverriddenMethod: { } overridden }:
                return overridden.OriginalDefinition;
            case IPropertySymbol { OverriddenProperty: { } overridden }:
                return overridden.OriginalDefinition;
            case IEventSymbol { OverriddenEvent: { } overridden }:
                return overridden.OriginalDefinition;
        }

        if (symbol.ContainingType is not { } containing)
        {
            return null;
        }

        foreach (var iface in containing.AllInterfaces)
        {
            foreach (var member in iface.GetMembers(symbol.Name))
            {
                if (SymbolEqualityComparer.Default.Equals(containing.FindImplementationForInterfaceMember(member), symbol))
                {
                    return member.OriginalDefinition;
                }
            }
        }

        return null;
    }

    private static XElement? Load(ISymbol symbol)
    {
        var text = symbol.OriginalDefinition.GetDocumentationCommentXml(expandIncludes: true);
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        try
        {
            var root = XElement.Parse(text.TrimStart().StartsWith("<member", StringComparison.Ordinal) ? text : $"<member>{text}</member>", LoadOptions.PreserveWhitespace);
            return root.HasElements ? root : null;
        }
        catch (XmlException)
        {
            return null;
        }
    }

    // ---- XML -> Markdown --------------------------------------------------------------------

    /// <summary>Block content: paragraphs, lists and code blocks separated by blank lines.</summary>
    public string? Block(XElement? element)
    {
        if (element is null)
        {
            return null;
        }

        var blocks = new List<string>();
        var paragraph = new StringBuilder();

        void Flush()
        {
            var text = CollapseWhitespace(paragraph.ToString());
            if (text.Length > 0)
            {
                blocks.Add(text);
            }

            paragraph.Clear();
        }

        foreach (var node in element.Nodes())
        {
            if (node is XElement { Name.LocalName: "para" or "p" } para)
            {
                Flush();
                if (Block(para) is { } inner)
                {
                    blocks.Add(inner);
                }
            }
            else if (node is XElement { Name.LocalName: "code" } code)
            {
                Flush();
                blocks.Add(CodeBlock(code));
            }
            else if (node is XElement { Name.LocalName: "list" } list)
            {
                Flush();
                blocks.Add(List(list));
            }
            else
            {
                paragraph.Append(InlineNode(node));
            }
        }

        Flush();
        return blocks.Count == 0 ? null : string.Join("\n\n", blocks);
    }

    /// <summary>Single-line content for list items and labeled rows.</summary>
    public string Inline(XElement element)
    {
        var block = Block(element);
        return block is null ? "" : block.Replace("\n\n", " ", StringComparison.Ordinal);
    }

    private string InlineNode(XNode node) =>
        node switch
        {
            XText text => Escape(text.Value),
            XElement e => e.Name.LocalName switch
            {
                "c" => Code(e.Value),
                "see" or "seealso" => LinkElement(e),
                "paramref" or "typeparamref" => Code(e.Attribute("name")?.Value ?? ""),
                "b" or "strong" => $"**{InlineChildren(e).Trim()}**",
                "i" or "em" => $"*{InlineChildren(e).Trim()}*",
                "br" => " ",
                "a" => $"[{InlineChildren(e).Trim()}]({e.Attribute("href")?.Value})",
                "code" => Code(e.Value.Trim()),
                "list" => " " + List(e).Replace('\n', ' ') + " ",
                "para" or "p" => " " + InlineChildren(e) + " ",
                _ => InlineChildren(e),
            },
            _ => "",
        };

    private string InlineChildren(XElement e) => string.Concat(e.Nodes().Select(InlineNode));

    private string LinkElement(XElement e)
    {
        var text = CollapseWhitespace(InlineChildren(e));
        if (e.Attribute("langword")?.Value is { } langword)
        {
            return Code(langword);
        }

        if (e.Attribute("href")?.Value is { } href)
        {
            return $"[{(text.Length > 0 ? text : href)}]({href})";
        }

        var cref = e.Attribute("cref")?.Value;
        return cref is null ? text : CrefLink(cref, text.Length > 0 ? text : null);
    }

    /// <summary>A Markdown link with an <c>xref:</c> placeholder resolved later by the writer.</summary>
    public string CrefLink(string? cref, string? text = null)
    {
        if (string.IsNullOrEmpty(cref))
        {
            return "";
        }

        var label = text ?? Code(CrefDisplay(cref));
        return $"[{label}](xref:{Uri.EscapeDataString(cref)})";
    }

    private string CrefDisplay(string cref)
    {
        var symbol = cref.Length > 2 && cref[1] == ':'
            ? DocumentationCommentId.GetFirstSymbolForDeclarationId(cref, compilation)
            : null;
        return symbol switch
        {
            INamedTypeSymbol type => type.ToDisplayString(Signatures.SignatureWriter.TypeNameFormat),
            IMethodSymbol { MethodKind: MethodKind.Constructor } ctor => ctor.ContainingType.Name,
            not null => symbol.Name,
            null => FallbackName(cref),
        };

        static string FallbackName(string cref)
        {
            var id = cref.Length > 2 && cref[1] == ':' ? cref[2..] : cref;
            var paren = id.IndexOf('(');
            if (paren >= 0)
            {
                id = id[..paren];
            }

            id = BacktickArity().Replace(id, "");
            return id[(id.LastIndexOf('.') + 1)..];
        }
    }

    private string List(XElement list)
    {
        var numbered = list.Attribute("type")?.Value == "number";
        var lines = list.Elements("item")
            .Select(
                (item, i) =>
                {
                    var term = item.Element("term");
                    var description = item.Element("description");
                    var text = term is not null && description is not null
                        ? $"**{Inline(term)}**: {Inline(description)}"
                        : Inline(description ?? term ?? item);
                    return (numbered ? $"{i + 1}. " : "* ") + text;
                }
            );
        return string.Join("\n", lines);
    }

    private static string CodeBlock(XElement code)
    {
        var language = code.Attribute("language")?.Value ?? code.Attribute("lang")?.Value ?? "csharp";
        var lines = code.Value.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n').ToList();
        while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[0]))
        {
            lines.RemoveAt(0);
        }

        while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[^1]))
        {
            lines.RemoveAt(lines.Count - 1);
        }

        var indent = lines.Where(l => l.Trim().Length > 0).Select(l => l.Length - l.TrimStart().Length).DefaultIfEmpty(0).Min();
        var body = string.Join("\n", lines.Select(l => l.Length >= indent ? l[indent..].TrimEnd() : l.Trim()));
        var fence = body.Contains("```", StringComparison.Ordinal) ? "````" : "```";
        return $"{fence}{language}\n{body}\n{fence}";
    }

    private static string Code(string text)
    {
        text = CollapseWhitespace(text);
        return text.Contains('`', StringComparison.Ordinal) ? $"`` {text} ``" : $"`{text}`";
    }

    private static string CollapseWhitespace(string text) => Whitespace().Replace(text, " ").Trim();

    private static string Escape(string text) => MarkdownSpecial().Replace(text, @"\$1");

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    // Authors use *emphasis* and `code` in plain doc text on purpose; only Markdoc tag syntax must not leak through.
    [GeneratedRegex(@"(\{%)")]
    private static partial Regex MarkdownSpecial();

    [GeneratedRegex(@"`+\d+")]
    private static partial Regex BacktickArity();
}
