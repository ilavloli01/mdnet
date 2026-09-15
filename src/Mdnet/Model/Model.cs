namespace Mdnet.Model;

/// <summary>One documented unit: a project (source mode) or a NuGet package (package mode).</summary>
public sealed record DocPackage(string Id, string? Version, PackageMetadata Metadata, IReadOnlyList<NamespaceDoc> Namespaces);

/// <summary>Package properties from the evaluated project (source mode) or the nuspec (package mode).</summary>
/// <param name="Frameworks">Target framework monikers, e.g. <c>net10.0</c>.</param>
public sealed record PackageMetadata(
    string? Title = null,
    string? Description = null,
    string? Authors = null,
    string? Company = null,
    string? Copyright = null,
    string? Tags = null,
    string? License = null,
    string? ProjectUrl = null,
    string? RepositoryUrl = null,
    IReadOnlyList<string>? Frameworks = null
)
{
    public static readonly PackageMetadata Empty = new();
}

public sealed record NamespaceDoc(string Name, IReadOnlyList<TypeDoc> Types);

/// <param name="DocId">Documentation comment id, e.g. <c>T:Core.Data.DbContext</c>.</param>
/// <param name="Name">Display name including containing types and type parameters, e.g. <c>Outer.Inner&lt;T&gt;</c>.</param>
/// <param name="Path">Page path relative to the package root, e.g. <c>Core.Data/DbContext.md</c>.</param>
/// <param name="Kind">C# kind keyword: class, struct, interface, enum, delegate, record, record struct.</param>
/// <param name="Badges">Accessibility and modifiers shown next to the kind, e.g. public, static.</param>
public sealed record TypeDoc(
    string DocId,
    string Name,
    string Namespace,
    string Path,
    string Kind,
    IReadOnlyList<string> Badges,
    string Declaration,
    DocComment Doc,
    IReadOnlyList<SectionDoc> Sections
);

/// <summary>A titled group of members, or of nested types when <see cref="NestedTypes"/> is set.</summary>
public sealed record SectionDoc(string Title, IReadOnlyList<MemberDoc> Members, IReadOnlyList<TypeDoc>? NestedTypes = null);

public sealed record MemberDoc(string DocId, string Anchor, string Signature, DocComment Doc);

/// <summary>Parsed XML doc comment. Every text is a Markdown fragment with <c>xref:</c> link placeholders.</summary>
public sealed record DocComment(
    string? Summary,
    IReadOnlyList<DocParam> TypeParams,
    IReadOnlyList<DocParam> Params,
    string? Value,
    string? Returns,
    IReadOnlyList<DocParam> Exceptions,
    string? Remarks,
    IReadOnlyList<string> Examples,
    IReadOnlyList<string> SeeAlso
)
{
    public static readonly DocComment Empty = new(null, [], [], null, null, [], null, [], []);
}

/// <param name="Name">Parameter name, or a Markdown cref link for exceptions.</param>
public sealed record DocParam(string Name, string Text);
