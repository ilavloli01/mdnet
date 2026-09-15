using Mdnet.Docs;
using Mdnet.Model;
using Mdnet.Signatures;
using Microsoft.CodeAnalysis;

namespace Mdnet.Extraction;

/// <summary>Builds the documentation model for one assembly. Shared by source and package mode.</summary>
public sealed class ApiExtractor(Compilation compilation, Visibility visibility)
{
    private static readonly string[] SectionOrder =
    [
        "Values",
        "Constructors",
        "Delegates",
        "Events",
        "Fields",
        "Properties",
        "Static Properties",
        "Static Methods",
        "Extension Methods",
        "Methods",
        "Operators",
        "Nested Types",
    ];

    private readonly XmlDocParser _parser = new(compilation);

    public DocPackage Extract(IAssemblySymbol assembly, string id, string? version, string? description)
    {
        var types = new List<INamedTypeSymbol>();
        CollectTypes(assembly.GlobalNamespace, types);

        var docs = new Dictionary<INamedTypeSymbol, TypeDoc?>(SymbolEqualityComparer.Default);
        // Nested types are referenced from their parent's "Nested Types" section, so build innermost first.
        foreach (var type in types.OrderByDescending(Depth))
        {
            docs[type] = BuildType(type, docs);
        }

        var namespaces = docs.Values.OfType<TypeDoc>()
            .GroupBy(t => t.Namespace)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => new NamespaceDoc(g.Key, g.OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase).ToList()))
            .ToList();
        return new DocPackage(id, version, description, namespaces);

        static int Depth(INamedTypeSymbol type)
        {
            var depth = 0;
            for (var t = type.ContainingType; t is not null; t = t.ContainingType)
            {
                depth++;
            }

            return depth;
        }
    }

    private void CollectTypes(INamespaceOrTypeSymbol container, List<INamedTypeSymbol> types)
    {
        if (container is INamespaceSymbol ns)
        {
            foreach (var child in ns.GetNamespaceMembers())
            {
                CollectTypes(child, types);
            }
        }

        foreach (var type in container.GetTypeMembers())
        {
            if (IsDocumentedType(type) && !(type.ContainingType is not null && type.TypeKind == TypeKind.Delegate))
            {
                types.Add(type);
                CollectTypes(type, types);
            }
        }
    }

    private bool IsDocumentedType(INamedTypeSymbol type) =>
        visibility.IsVisible(type)
        && !type.IsImplicitlyDeclared
        && type.CanBeReferencedByName
        && type.TypeKind.ToString() != "Extension"
        && !HasAttribute(type, "System.Runtime.CompilerServices.CompilerGeneratedAttribute")
        && !HasAttribute(type, "Microsoft.CodeAnalysis.EmbeddedAttribute");

    private TypeDoc BuildType(INamedTypeSymbol type, IReadOnlyDictionary<INamedTypeSymbol, TypeDoc?> built)
    {
        var groups = new Dictionary<string, List<MemberDoc>>();
        var anchors = new HashSet<string>(StringComparer.Ordinal);
        var nested = new List<TypeDoc>();

        foreach (var member in type.GetMembers())
        {
            if (member is INamedTypeSymbol nestedType)
            {
                if (nestedType.TypeKind != TypeKind.Delegate && built.TryGetValue(nestedType, out var nestedDoc) && nestedDoc is not null)
                {
                    nested.Add(nestedDoc);
                    continue;
                }

                if (nestedType.TypeKind != TypeKind.Delegate || !IsDocumentedType(nestedType))
                {
                    continue;
                }
            }
            else if (!visibility.IsVisible(member) || IsHidden(member))
            {
                continue;
            }

            var section = SectionOf(member, type);
            if (section is null)
            {
                continue;
            }

            var anchor = UniqueAnchor(AnchorBase(member), anchors);
            var signature = SignatureWriter.MemberSignature(member, visibility);
            if (!groups.TryGetValue(section, out var list))
            {
                groups[section] = list = [];
            }

            list.Add(new MemberDoc(DocumentationId(member), anchor, signature, _parser.Parse(member)));
        }

        var sections = SectionOrder
            .Select(title =>
                title == "Nested Types"
                    ? nested.Count > 0 ? new SectionDoc(title, [], nested) : null
                    : groups.TryGetValue(title, out var members) ? new SectionDoc(title, members) : null
            )
            .OfType<SectionDoc>()
            .ToList();

        return new TypeDoc(
            DocId: DocumentationId(type),
            Name: type.ToDisplayString(SignatureWriter.TypeNameFormat),
            Namespace: NamespaceName(type),
            Path: $"{NamespaceName(type)}/{FileName(type)}.md",
            Kind: SignatureWriter.TypeKind(type),
            Badges: [SignatureWriter.Accessibility(type.DeclaredAccessibility), .. SignatureWriter.TypeModifiers(type)],
            Declaration: SignatureWriter.TypeDeclaration(type),
            Doc: _parser.Parse(type),
            Sections: sections
        );
    }

    private static string? SectionOf(ISymbol member, INamedTypeSymbol type) =>
        member switch
        {
            INamedTypeSymbol => "Delegates",
            IFieldSymbol when type.TypeKind == TypeKind.Enum => "Values",
            IFieldSymbol => "Fields",
            IEventSymbol => "Events",
            IPropertySymbol { IsStatic: true } => "Static Properties",
            IPropertySymbol => "Properties",
            IMethodSymbol { MethodKind: MethodKind.Constructor } => "Constructors",
            IMethodSymbol { MethodKind: MethodKind.UserDefinedOperator or MethodKind.Conversion } => "Operators",
            IMethodSymbol { MethodKind: MethodKind.Ordinary, IsExtensionMethod: true } => "Extension Methods",
            IMethodSymbol { MethodKind: MethodKind.Ordinary, IsStatic: true } => "Static Methods",
            IMethodSymbol { MethodKind: MethodKind.Ordinary } => "Methods",
            _ => null,
        };

    private static bool IsHidden(ISymbol member)
    {
        if (member.Name.StartsWith('<') || member is IFieldSymbol { Name: "value__" })
        {
            return true;
        }

        if (HasAttribute(member, "System.Runtime.CompilerServices.CompilerGeneratedAttribute"))
        {
            return true;
        }

        if (member.IsImplicitlyDeclared)
        {
            // Keep the implicit default constructor of classes: metadata always has it.
            return !(
                member is IMethodSymbol { MethodKind: MethodKind.Constructor, IsStatic: false, Parameters.Length: 0 } ctor
                && ctor.ContainingType is { TypeKind: TypeKind.Class, IsStatic: false, IsRecord: false }
            );
        }

        return false;
    }

    private static bool HasAttribute(ISymbol symbol, string fullName) =>
        symbol.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() == fullName);

    private static string AnchorBase(ISymbol member) =>
        member switch
        {
            IMethodSymbol { MethodKind: MethodKind.Constructor } => "ctor",
            IPropertySymbol { IsIndexer: true } => "indexer",
            _ => new string(member.Name.ToLowerInvariant().Select(c => char.IsAsciiLetterOrDigit(c) ? c : '-').ToArray()).Trim('-'),
        };

    private static string UniqueAnchor(string anchor, HashSet<string> used)
    {
        var candidate = anchor;
        for (var i = 2; !used.Add(candidate); i++)
        {
            candidate = $"{anchor}-{i}";
        }

        return candidate;
    }

    public static string DocumentationId(ISymbol symbol) =>
        symbol.OriginalDefinition.GetDocumentationCommentId() ?? symbol.ToDisplayString();

    public static string NamespaceName(INamedTypeSymbol type) =>
        type.ContainingNamespace is { IsGlobalNamespace: false } ns ? ns.ToDisplayString() : "global";

    /// <summary><c>Outer-1.Inner</c> for <c>Outer&lt;T&gt;.Inner</c>.</summary>
    public static string FileName(INamedTypeSymbol type)
    {
        var name = type.Arity > 0 ? $"{type.Name}-{type.Arity}" : type.Name;
        return type.ContainingType is null ? name : $"{FileName(type.ContainingType)}.{name}";
    }
}
