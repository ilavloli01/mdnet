using Mdnet.Docs;
using Mdnet.Extraction;
using Mdnet.Markdown;
using Mdnet.Signatures;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Mdnet.Tests;

public class SignatureTests
{
    private const string Source = """
        #nullable enable
        using System;
        using System.Collections.Generic;
        using System.Diagnostics.CodeAnalysis;

        namespace Edge;

        /// <summary>Edge cases.</summary>
        public abstract class Widget<T> where T : notnull
        {
            public const string Name = "widget\n";
            public required int Size { get; init; }
            public int this[int index] { get => 0; protected set { } }
            public abstract ref readonly T Peek();
            public static bool TryParse(string? text, out int value, ref int cursor, in DateTime at, params string[] tags) { value = 0; return true; }
            public void Defaults(DayOfWeek day = DayOfWeek.Friday, double ratio = 0.5, char c = 'x', int? maybe = null, object? o = null) { }
            public void @event(int @class) { }
            [Obsolete("Use Other")]
            public virtual void Old() { }
            public TOut Map<TOut, TState>(Func<T, TState, TOut> map, TState state) where TOut : class?, new() where TState : struct => throw null!;
            public static explicit operator string(Widget<T> w) => "";
            public static Widget<T> operator checked -(Widget<T> w) => w;
            public static Widget<T> operator -(Widget<T> w) => w;
            protected internal int Shared;
            private protected int Hidden;
            internal int Internal;
        }

        public interface IShape<in TIn, out TOut>
        {
            TOut Area(TIn input);
            static abstract IShape<TIn, TOut> Create();
            int Sides { get; }
        }

        public enum Small : byte { A = 1, B = 2 }

        public ref struct Span2 { public readonly int Length => 0; }

        public sealed class Sealed : Widget<string>
        {
            public override ref readonly string Peek() => throw null!;
            public sealed override void Old() { }
        }

        /// <summary>Handles <paramref name="value"/>.</summary>
        public delegate TResult Handler<in TArg, out TResult>(TArg value) where TResult : allows ref struct;
        """;

    private static (CSharpCompilation Compilation, INamedTypeSymbol[] Types) Compile()
    {
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Select(p => MetadataReference.CreateFromFile(p));
        var compilation = CSharpCompilation.Create(
            "Edge",
            [CSharpSyntaxTree.ParseText(Source, new CSharpParseOptions(LanguageVersion.Preview, DocumentationMode.Diagnose))],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable)
        );
        var types = compilation.Assembly.GlobalNamespace.GetNamespaceMembers().Single(n => n.Name == "Edge").GetTypeMembers().ToArray();
        return (compilation, types);
    }

    [Test]
    public async Task Signatures_cover_csharp_edge_cases()
    {
        var (compilation, types) = Compile();
        var visibility = new Visibility(VisibilityLevel.Protected);
        var lines = new List<string>();
        foreach (var type in types.OrderBy(t => t.Name, StringComparer.Ordinal))
        {
            lines.Add("// " + type.Name);
            lines.Add(SignatureWriter.TypeDeclaration(type));
            foreach (var member in type.GetMembers().Where(m => visibility.IsVisible(m) && !m.IsImplicitlyDeclared))
            {
                if (member is IMethodSymbol { MethodKind: MethodKind.PropertyGet or MethodKind.PropertySet or MethodKind.DelegateInvoke })
                {
                    continue;
                }

                lines.Add(SignatureWriter.MemberSignature(member, visibility));
            }

            lines.Add("");
        }

        await Assert.That(compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error)).IsEmpty();
        await Snapshot.Match(string.Join("\n", lines), "txt");
    }

    [Test]
    public async Task Internal_visibility_includes_internal_members()
    {
        var (_, types) = Compile();
        var widget = types.Single(t => t.Name == "Widget");

        var protectedNames = widget.GetMembers().Where(new Visibility(VisibilityLevel.Protected).IsVisible).Select(m => m.Name).ToList();
        var internalNames = widget.GetMembers().Where(new Visibility(VisibilityLevel.Internal).IsVisible).Select(m => m.Name).ToList();

        await Assert.That(protectedNames).Contains("Shared");
        await Assert.That(protectedNames).DoesNotContain("Hidden");
        await Assert.That(protectedNames).DoesNotContain("Internal");
        await Assert.That(internalNames).Contains("Hidden");
        await Assert.That(internalNames).Contains("Internal");
    }

    [Test]
    public async Task Delegate_docs_resolve_paramref()
    {
        var (compilation, types) = Compile();
        var doc = new XmlDocParser(compilation).Parse(types.Single(t => t.Name == "Handler"));

        await Assert.That(doc.Summary).IsEqualTo("Handles `value`.");
    }

    [Test]
    public async Task Relative_links_between_pages()
    {
        await Assert.That(MarkdownWriter.Relative("Pkg/A.B/Type.md", "Pkg/A.B/Other.md")).IsEqualTo("Other.md");
        await Assert.That(MarkdownWriter.Relative("Pkg/A.B/Type.md", "Pkg/C/Other.md")).IsEqualTo("../C/Other.md");
        await Assert.That(MarkdownWriter.Relative("Pkg/index.md", "Other/X/Y.md")).IsEqualTo("../Other/X/Y.md");
    }

    [Test]
    public async Task File_names_encode_arity_and_nesting()
    {
        var (_, types) = Compile();
        await Assert.That(ApiExtractor.FileName(types.Single(t => t.Name == "Widget"))).IsEqualTo("Widget-1");
        await Assert.That(ApiExtractor.FileName(types.Single(t => t.Name == "Handler"))).IsEqualTo("Handler-2");
    }
}
