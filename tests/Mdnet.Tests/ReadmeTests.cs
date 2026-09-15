using Mdnet.Markdown;

namespace Mdnet.Tests;

public class ReadmeTests
{
    [Test]
    public async Task Import_drops_repeated_title_demotes_headings_and_repository_links()
    {
        var readme = """
            # Contoso STORAGE

            Intro with a [relative link](../docs/adr.md), an [absolute one](https://x.dev) and an [anchor](#usage).
            ![diagram](img/flow.png) ![badge](https://img.shields.io/x.svg)

            ## Usage

            ```bash
            # a comment, not a heading
            echo "{% not a tag %}"
            ```

            Avoid {% tags %} in prose.
            """;

        var imported = ReadmeImporter.Import(readme, 3, "Contoso.Storage");

        await Assert.That(imported).IsEqualTo(
            """
            Intro with a relative link, an [absolute one](https://x.dev) and an [anchor](#usage).
             ![badge](https://img.shields.io/x.svg)

            ### Usage

            ```bash
            # a comment, not a heading
            echo "{% not a tag %}"
            ```

            Avoid \{% tags %} in prose.
            """.ReplaceLineEndings("\n")
        );
    }

    [Test]
    public async Task Import_keeps_a_meaningful_title()
    {
        var imported = ReadmeImporter.Import("# Distributed write locks\n\nText.\n\n## Cast\n", 3, "Acme.Locking");

        await Assert.That(imported).IsEqualTo("### Distributed write locks\n\nText.\n\n#### Cast");
    }

    [Test]
    public async Task First_paragraph_is_plain_text()
    {
        var brief = ReadmeImporter.FirstParagraph("### Title\n\n```\ncode\n```\n\nUses **bold**, `code` and [links](x.md)\nacross lines.\n\nSecond.");

        await Assert.That(brief).IsEqualTo("Uses bold, code and links across lines.");
    }

    [Test]
    public async Task Namespace_tree_groups_nested_namespaces_under_root_features()
    {
        string[] namespaces =
        [
            "Contoso",
            "Contoso.Domain.Entities",
            "Contoso.Domain.Events",
            "Contoso.Modularity",
            "Contoso.Modularity.Features",
            "Microsoft.Extensions.DependencyInjection",
        ];

        var tree = NamespaceTree.Build(namespaces, "Contoso");

        await Assert.That(tree.Select(f => $"{f.Title}: {string.Join(", ", f.Namespaces)}").ToArray()).IsEquivalentTo(
            [
                "Contoso: Contoso",
                "Domain: Contoso.Domain.Entities, Contoso.Domain.Events",
                "Modularity: Contoso.Modularity, Contoso.Modularity.Features",
                "Microsoft.Extensions.DependencyInjection: Microsoft.Extensions.DependencyInjection",
            ],
            TUnit.Assertions.Enums.CollectionOrdering.Matching
        );
    }

    [Test]
    public async Task Namespace_tree_falls_back_to_the_common_prefix()
    {
        var tree = NamespaceTree.Build(["Core.Data", "Core.Data.Repositories", "Core.Data.Repositories.Sql"], rootNamespace: null);

        await Assert.That(tree.Select(f => $"{f.Title}: {string.Join(", ", f.Namespaces)}").ToArray()).IsEquivalentTo(
            ["Core.Data: Core.Data", "Repositories: Core.Data.Repositories, Core.Data.Repositories.Sql"],
            TUnit.Assertions.Enums.CollectionOrdering.Matching
        );
    }
}
