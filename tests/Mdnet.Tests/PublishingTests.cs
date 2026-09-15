using Mdnet.Loading;
using Mdnet.Publishing;

namespace Mdnet.Tests;

public class PublishingTests
{
    private static Manifest Publish(string dir, string version, Dictionary<string, string> files) =>
        DocsFolder.Replace(dir, files, new Manifest("Pkg", version));

    [Test]
    public async Task Install_copies_then_reports_up_to_date_and_removes_stale_files()
    {
        var work = TestPaths.TempDirectory("install");
        var published = Path.Combine(work, "published", "Pkg");
        var root = Path.Combine(work, "local");

        Publish(published, "1.0.0", new() { ["index.md"] = "# Pkg 1.0.0\n\n> Demo.\n", ["Ns/Old.md"] = "old", ["Ns/Type.md"] = "v1" });
        var (first, _) = await DocsInstaller.InstallAsync(new DirectoryDocsSource(published), root, Log.Silent, CancellationToken.None);
        var (second, _) = await DocsInstaller.InstallAsync(new DirectoryDocsSource(published), root, Log.Silent, CancellationToken.None);
        File.AppendAllText(Path.Combine(root, "Pkg", "Ns", "Old.md"), " edited locally");
        var (repaired, _) = await DocsInstaller.InstallAsync(new DirectoryDocsSource(published), root, Log.Silent, CancellationToken.None);
        var repairedText = File.ReadAllText(Path.Combine(root, "Pkg", "Ns", "Old.md"));

        Publish(published, "1.1.0", new() { ["index.md"] = "# Pkg 1.1.0\n", ["Ns/Type.md"] = "v2" });
        var (third, manifest) = await DocsInstaller.InstallAsync(new DirectoryDocsSource(published), root, Log.Silent, CancellationToken.None);
        RootIndex.Write(root);

        await Assert.That(first).IsEqualTo(InstallResult.Updated);
        await Assert.That(second).IsEqualTo(InstallResult.UpToDate);
        await Assert.That(repaired).IsEqualTo(InstallResult.Updated);
        await Assert.That(repairedText).IsEqualTo("old");
        await Assert.That(third).IsEqualTo(InstallResult.Updated);
        await Assert.That(manifest!.Version).IsEqualTo("1.1.0");
        await Assert.That(File.ReadAllText(Path.Combine(root, "Pkg", "Ns", "Type.md"))).IsEqualTo("v2");
        await Assert.That(File.Exists(Path.Combine(root, "Pkg", "Ns", "Old.md"))).IsFalse();
        await Assert.That(File.ReadAllText(Path.Combine(root, "index.md"))).Contains("* [Pkg](Pkg/index.md) `1.1.0`");
    }

    [Test]
    public async Task Root_index_groups_packages_and_reads_frontmatter()
    {
        var root = TestPaths.TempDirectory("root-index");
        void Package(string id, string frontmatter) =>
            DocsFolder.Replace(Path.Combine(root, id), new Dictionary<string, string> { ["index.md"] = frontmatter + $"# {id}\n" }, new Manifest(id, "2.0.0"));

        Package("Acme.Locking", Frontmatter.Write([new("description", "Locks: distributed #1"), new("types", "3")]));
        Package("Acme.Workflows", Frontmatter.Write([new("types", "1")]));
        Package("Contoso", "");
        RootIndex.Write(root);

        await Assert.That(File.ReadAllText(Path.Combine(root, "index.md"))).IsEqualTo(
            """
            # API documentation

            3 packages · 4 types

            ## Acme

            * [Acme.Locking](Acme.Locking/index.md) `2.0.0`: Locks: distributed #1
            * [Acme.Workflows](Acme.Workflows/index.md) `2.0.0`

            ## Contoso

            * [Contoso](Contoso/index.md) `2.0.0`

            """.ReplaceLineEndings("\n")
        );
    }

    [Test]
    public async Task Frontmatter_quotes_only_ambiguous_values()
    {
        var text = Frontmatter.Write([new("a", "plain value, net10.0"), new("b", "key: value"), new("c", "- dash"), new("d", " "), new("e", "\"quoted\" start")]);
        var parsed = Frontmatter.Parse(text + "# Title\n");

        await Assert.That(text).IsEqualTo("---\na: plain value, net10.0\nb: \"key: value\"\nc: \"- dash\"\ne: \"\\\"quoted\\\" start\"\n---\n");
        await Assert.That(parsed["b"]).IsEqualTo("key: value");
        await Assert.That(parsed["e"]).IsEqualTo("\"quoted\" start");
        await Assert.That(parsed.ContainsKey("d")).IsFalse();
    }

    [Test]
    public async Task Install_rejects_tampered_files()
    {
        var work = TestPaths.TempDirectory("tamper");
        var published = Path.Combine(work, "Pkg");
        Publish(published, "1.0.0", new() { ["index.md"] = "# Pkg\n" });
        File.WriteAllText(Path.Combine(published, "index.md"), "changed");

        await Assert.That(async () => await DocsInstaller.InstallAsync(new DirectoryDocsSource(published), Path.Combine(work, "out"), Log.Silent, CancellationToken.None))
            .Throws<InvalidDataException>();
    }

    [Test]
    [Arguments("../escape.md")]
    [Arguments("/abs.md")]
    [Arguments("C:/x.md")]
    [Arguments("a\\b.md")]
    [Arguments("a//b.md")]
    public async Task Manifest_rejects_unsafe_paths(string path)
    {
        var json = $$"""{ "schema": 1, "id": "Pkg", "files": { "{{path.Replace("\\", "\\\\")}}": "00" } }""";
        await Assert.That(() => Manifest.Parse(json)).Throws<InvalidDataException>();
    }

    [Test]
    public async Task Manifest_rejects_newer_schema()
    {
        await Assert.That(() => Manifest.Parse("""{ "schema": 99, "id": "Pkg", "files": {} }""")).Throws<InvalidDataException>();
    }

    [Test]
    public async Task Git_checkout_downloads_docs_at_a_tag()
    {
        if (ProcessRunner.FindOnPath("git") is null)
        {
            Skip.Test("git not installed");
        }

        var work = TestPaths.TempDirectory("git");
        var repo = Path.Combine(work, "repo");
        Publish(Path.Combine(repo, "docs", "Pkg"), "2.0.0", new() { ["index.md"] = "# Pkg 2.0.0\n" });
        await File.WriteAllTextAsync(Path.Combine(repo, "README.md"), "not docs");
        foreach (var args in new[]
        {
            new[] { "init", "-q", "-b", "main" },
            ["-c", "user.name=t", "-c", "user.email=t@t", "add", "."],
            ["-c", "user.name=t", "-c", "user.email=t@t", "commit", "-q", "-m", "docs"],
            ["tag", "v2.0.0"],
        })
        {
            var result = await ProcessRunner.RunAsync("git", args, repo);
            await Assert.That(result.Success).IsTrue().Because(result.Error);
        }

        using var checkouts = new GitCheckouts(Log.Silent);
        var url = new Uri(repo).AbsoluteUri;
        var checkout = await checkouts.CheckoutAsync(url, "v2.0.0", ["docs/Pkg"], CancellationToken.None);
        var (status, manifest) = await DocsInstaller.InstallAsync(new DirectoryDocsSource(Path.Combine(checkout, "docs", "Pkg")), Path.Combine(work, "out"), Log.Silent, CancellationToken.None);

        await Assert.That(status).IsEqualTo(InstallResult.Updated);
        await Assert.That(manifest!.Version).IsEqualTo("2.0.0");
        await Assert.That(File.Exists(Path.Combine(checkout, "README.md"))).IsFalse();
    }

    [Test]
    public async Task Sources_config_expands_templates()
    {
        await Assert.That(SourcesConfig.Expand("https://x/{idLower}/{version}/{id}", "Acme.Core", "1.2.0")).IsEqualTo("https://x/acme.core/1.2.0/Acme.Core");
    }

    [Test]
    public async Task Glob_and_version_ordering()
    {
        var glob = new Glob(["Acme.*;Other"]);
        await Assert.That(glob.IsMatch("acme.core")).IsTrue();
        await Assert.That(glob.IsMatch("Other")).IsTrue();
        await Assert.That(glob.IsMatch("AcmeCore")).IsFalse();

        string[] versions = ["1.10.0", "1.2.0", "1.10.0-beta.1", "1.9.9"];
        await Assert.That(versions.Order(PackageVersionComparer.Instance).ToArray()).IsEquivalentTo(["1.2.0", "1.9.9", "1.10.0-beta.1", "1.10.0"]);
    }
}
