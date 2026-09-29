using System.Xml.Linq;
using BrainSharp.NugetCheck.Entities;
using BrainSharp.NugetCheck.Tests.Fakes;

namespace BrainSharp.NugetCheck.Tests.Unit;

public class ProjectFileTests
{
    private string _directory = null!;

    [SetUp]
    public void SetUp() => _directory = TestDirectories.Create();

    [TearDown]
    public void TearDown() => TestDirectories.Delete(_directory);

    private static (string Id, string? Version)[] Read(string xml) =>
        NugetCheck.ReadPackageReferences(XDocument.Parse(xml)).Select(x => (x.NugetPackageId, x.Version)).ToArray();

    private string WriteProject(string xml)
    {
        var path = Path.Combine(_directory, "Project.csproj");
        File.WriteAllText(path, xml);
        return path;
    }

    [Test]
    public void ReadPackageReferences_SdkStyleProject_ReadsVersionAttributes()
    {
        var references = Read("""
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <PackageReference Include="A" Version="1.0.0" />
                <PackageReference Include="B" Version="2.0.0" />
              </ItemGroup>
            </Project>
            """);

        Assert.That(references, Is.EqualTo(new (string, string?)[] { ("A", "1.0.0"), ("B", "2.0.0") }));
    }

    [Test]
    public void ReadPackageReferences_NamespacedProjectWithVersionElement_ReadsReferences()
    {
        var references = Read("""
            <Project ToolsVersion="15.0" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
              <ItemGroup>
                <PackageReference Include="A">
                  <Version> 1.2.3 </Version>
                </PackageReference>
              </ItemGroup>
            </Project>
            """);

        Assert.That(references, Is.EqualTo(new (string, string?)[] { ("A", "1.2.3") }));
    }

    [Test]
    public void ReadPackageReferences_ConditionalItemGroup_IsIncluded()
    {
        var references = Read("""
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup Condition="'$(TargetFramework)' == 'net8.0'">
                <PackageReference Include="A" Version="1.0.0" />
              </ItemGroup>
            </Project>
            """);

        Assert.That(references, Is.EqualTo(new (string, string?)[] { ("A", "1.0.0") }));
    }

    [Test]
    public void ReadPackageReferences_MissingVersion_ReturnsNullVersion()
    {
        var references = Read("""
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <PackageReference Include="A" />
              </ItemGroup>
            </Project>
            """);

        Assert.That(references, Is.EqualTo(new (string, string?)[] { ("A", null) }));
    }

    [Test]
    public void ReadPackageReferences_UpdateOnlyReference_IsIgnored()
    {
        var references = Read("""
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <PackageReference Update="A" Version="2.0.0" />
              </ItemGroup>
            </Project>
            """);

        Assert.That(references, Is.Empty);
    }

    [Test]
    public async Task CheckPackageAndTransientsAsync_ProjectWithoutVersion_ReturnsVersionNotSpecified()
    {
        var source = new FakeMetadataSource().WithPackage("A", "1.0.0");
        var path = WriteProject("""
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <PackageReference Include="A" />
              </ItemGroup>
            </Project>
            """);

        var results = await new NugetCheck(source, new InMemoryPackageCache()).CheckPackageAndTransientsAsync(path);

        Assert.Multiple(() =>
        {
            Assert.That(results.TotalWarnings, Is.EqualTo(1));
            Assert.That(results.PackageReferences.Single().Warnings.Single().Message, Is.EqualTo(WarningMessages.VersionNotSpecified));
            Assert.That(source.CallsFor("A"), Is.EqualTo(0));
        });
    }

    [Test]
    public async Task CheckPackageAndTransientsAsync_ProjectWithPropertyVersion_ReturnsVersionNotFound()
    {
        var source = new FakeMetadataSource().WithPackage("A", "1.0.0");
        var path = WriteProject("""
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <PackageReference Include="A" Version="$(AVersion)" />
              </ItemGroup>
            </Project>
            """);

        var results = await new NugetCheck(source, new InMemoryPackageCache()).CheckPackageAndTransientsAsync(path);

        Assert.That(results.PackageReferences.Single().Warnings.Single().Message, Is.EqualTo(WarningMessages.VersionNotFound));
    }

    [Test]
    public async Task CheckPackageAndTransientsAsync_TwoReferencesSharingVulnerableDependency_CountsBoth()
    {
        var source = new FakeMetadataSource()
            .WithPackage("R1", "1.0.0", dependencies: ["D [1.0.0, )"])
            .WithPackage("R2", "1.0.0", dependencies: ["D [1.0.0, )"])
            .WithPackage("D", "1.0.0", vulnerable: true);
        var path = WriteProject("""
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <PackageReference Include="R1" Version="1.0.0" />
                <PackageReference Include="R2" Version="1.0.0" />
              </ItemGroup>
            </Project>
            """);

        var results = await new NugetCheck(source, new InMemoryPackageCache()).CheckPackageAndTransientsAsync(path);

        Assert.That(results.TotalWarnings, Is.EqualTo(2));
    }
}
