using Mdnet.Loading;

namespace Mdnet.Tests;

public class MetadataTests
{
    [Test]
    public async Task Project_metadata_includes_properties_inherited_from_directory_build_props()
    {
        var work = TestPaths.TempDirectory("metadata");
        await File.WriteAllTextAsync(
            Path.Combine(work, "Directory.Build.props"),
            """
            <Project>
              <PropertyGroup>
                <TargetFrameworks>netstandard2.1;net10.0</TargetFrameworks>
                <Authors>Framework team</Authors>
                <PackageLicenseExpression>MIT</PackageLicenseExpression>
              </PropertyGroup>
            </Project>
            """
        );
        await File.WriteAllTextAsync(Path.Combine(work, "Directory.Packages.props"), "<Project />");
        var project = Path.Combine(work, "Lib", "Lib.csproj");
        Directory.CreateDirectory(Path.GetDirectoryName(project)!);
        await File.WriteAllTextAsync(
            project,
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <PackageId>Acme.Lib</PackageId>
                <Description>
                  Spans
                  lines.
                </Description>
              </PropertyGroup>
            </Project>
            """
        );

        var info = (await MsBuildProperties.ReadAsync([project], Log.Silent, CancellationToken.None))[project];

        await Assert.That(info.Id).IsEqualTo("Acme.Lib");
        await Assert.That(info.Version).IsNull();
        await Assert.That(info.Metadata.Description).IsEqualTo("Spans lines.");
        await Assert.That(info.Metadata.Authors).IsEqualTo("Framework team");
        await Assert.That(info.Metadata.Company).IsNull();
        await Assert.That(info.Metadata.License).IsEqualTo("MIT");
        await Assert.That(info.Metadata.Frameworks!).IsEquivalentTo(["net10.0", "netstandard2.1"]);
    }
}
