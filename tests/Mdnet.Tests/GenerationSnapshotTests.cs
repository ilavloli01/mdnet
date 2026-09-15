using Mdnet.Loading;
using Mdnet.Markdown;
using Mdnet.Signatures;

namespace Mdnet.Tests;

public class GenerationSnapshotTests
{
    [Test]
    public async Task Source_mode_markdown()
    {
        var packages = await new SourceLoader(Log.Silent).LoadAsync(
            TestPaths.SampleProject,
            new Glob([]),
            new Glob([]),
            new Visibility(VisibilityLevel.Protected),
            CancellationToken.None
        );

        var output = TestPaths.TempDirectory("source");
        new MarkdownWriter().Write(packages, output);

        await Snapshot.Match(TestPaths.Snapshot(output), "md");
    }

    [Test]
    public async Task Package_mode_markdown()
    {
        var work = TestPaths.TempDirectory("package");
        var feed = Path.Combine(work, "feed");
        var pack = await ProcessRunner.RunAsync("dotnet", ["pack", TestPaths.SampleProject, "-c", "Release", "-o", feed, "-p:ContinuousIntegrationBuild=true"]);
        await Assert.That(pack.Success).IsTrue().Because(pack.Output + pack.Error);

        var consumer = Path.Combine(work, "consumer");
        Directory.CreateDirectory(consumer);
        await File.WriteAllTextAsync(
            Path.Combine(consumer, "Consumer.csproj"),
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <RestorePackagesPath>packages</RestorePackagesPath>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="Sample.Lib" Version="1.2.3" />
              </ItemGroup>
            </Project>
            """
        );
        await File.WriteAllTextAsync(
            Path.Combine(consumer, "nuget.config"),
            $"""
            <configuration>
              <packageSources>
                <clear />
                <add key="feed" value="{feed}" />
              </packageSources>
            </configuration>
            """
        );
        // Isolate from repository-level Directory.Build.props / central package management.
        await File.WriteAllTextAsync(Path.Combine(work, "Directory.Build.props"), "<Project />");
        await File.WriteAllTextAsync(Path.Combine(work, "Directory.Packages.props"), "<Project />");

        var packages = await new PackageLoader(Log.Silent).LoadAsync(
            consumer,
            new Glob(["Sample.*"]),
            new Visibility(VisibilityLevel.Protected),
            CancellationToken.None
        );

        var output = Path.Combine(work, "docs");
        new MarkdownWriter().Write(packages, output);

        await Snapshot.Match(TestPaths.Snapshot(output), "md");
    }
}
