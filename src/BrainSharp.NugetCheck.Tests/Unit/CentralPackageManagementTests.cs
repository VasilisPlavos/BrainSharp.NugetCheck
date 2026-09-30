using BrainSharp.NugetCheck.Entities;
using BrainSharp.NugetCheck.Tests.Fakes;

namespace BrainSharp.NugetCheck.Tests.Unit;

public class CentralPackageManagementTests
{
    private string _directory = null!;
    private FakeMetadataSource _source = null!;

    [SetUp]
    public void SetUp()
    {
        _directory = TestDirectories.Create();
        _source = new FakeMetadataSource()
            .WithPackage("A", "1.0.0")
            .WithPackage("A", "2.0.0")
            .WithPackage("A", "3.0.0")
            .WithPackage("G", "1.0.0");
    }

    [TearDown]
    public void TearDown() => TestDirectories.Delete(_directory);

    private string Write(string relativePath, string xml)
    {
        var path = Path.Combine(_directory, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, xml);
        return path;
    }

    private static string Props(string items, string properties = "<ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>") => $"""
        <Project>
          <PropertyGroup>{properties}</PropertyGroup>
          <ItemGroup>{items}</ItemGroup>
        </Project>
        """;

    private static string Project(string items, string properties = "") => $"""
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>{properties}</PropertyGroup>
          <ItemGroup>{items}</ItemGroup>
        </Project>
        """;

    private async Task<(string Id, string? Version, string? FirstWarning)[]> ScanAsync(string projectPath)
    {
        var results = await new NugetCheck(_source, new InMemoryPackageCache()).CheckPackageAndTransientsAsync(projectPath);
        return results.PackageReferences
            .Select(x => (x.NugetPackageId, x.NugetPackageOriginalVersion, x.Warnings.FirstOrDefault()?.Message))
            .ToArray();
    }

    [Test]
    public async Task CheckPackageAndTransientsAsync_PropsInProjectFolder_UsesCentralVersion()
    {
        Write("Directory.Packages.props", Props("""<PackageVersion Include="A" Version="2.0.0" />"""));
        var project = Write("Project.csproj", Project("""<PackageReference Include="A" />"""));

        Assert.That(await ScanAsync(project), Is.EqualTo(new (string, string?, string?)[] { ("A", "2.0.0", null) }));
    }

    [Test]
    public async Task CheckPackageAndTransientsAsync_PropsInParentFolder_UsesCentralVersion()
    {
        Write("Directory.Packages.props", Props("""<PackageVersion Include="A" Version="2.0.0" />"""));
        var project = Write(Path.Combine("src", "App", "App.csproj"), Project("""<PackageReference Include="A" />"""));

        Assert.That(await ScanAsync(project), Is.EqualTo(new (string, string?, string?)[] { ("A", "2.0.0", null) }));
    }

    [Test]
    public async Task CheckPackageAndTransientsAsync_PropsOnTwoLevels_UsesOnlyNearest()
    {
        Write("Directory.Packages.props", Props("""<PackageVersion Include="A" Version="1.0.0" /><PackageVersion Include="G" Version="1.0.0" />"""));
        Write(Path.Combine("src", "Directory.Packages.props"), Props("""<PackageVersion Include="A" Version="2.0.0" />"""));
        var project = Write(Path.Combine("src", "App", "App.csproj"), Project("""<PackageReference Include="A" /><PackageReference Include="G" />"""));

        Assert.That(await ScanAsync(project), Is.EqualTo(new (string, string?, string?)[]
        {
            ("A", "2.0.0", null),
            ("G", null, WarningMessages.VersionNotSpecified)
        }));
    }

    [Test]
    public async Task CheckPackageAndTransientsAsync_VersionOverride_WinsOverCentralVersion()
    {
        Write("Directory.Packages.props", Props("""<PackageVersion Include="A" Version="2.0.0" />"""));
        var project = Write("Project.csproj", Project("""<PackageReference Include="A" VersionOverride="3.0.0" />"""));

        Assert.That(await ScanAsync(project), Is.EqualTo(new (string, string?, string?)[] { ("A", "3.0.0", null) }));
    }

    [Test]
    public async Task CheckPackageAndTransientsAsync_VersionOverrideElement_WinsOverCentralVersion()
    {
        Write("Directory.Packages.props", Props("""<PackageVersion Include="A" Version="2.0.0" />"""));
        var project = Write("Project.csproj", Project("""<PackageReference Include="A"><VersionOverride>3.0.0</VersionOverride></PackageReference>"""));

        Assert.That(await ScanAsync(project), Is.EqualTo(new (string, string?, string?)[] { ("A", "3.0.0", null) }));
    }

    [Test]
    public async Task CheckPackageAndTransientsAsync_VersionOnReference_WinsOverOverrideAndCentralVersion()
    {
        Write("Directory.Packages.props", Props("""<PackageVersion Include="A" Version="2.0.0" />"""));
        var project = Write("Project.csproj", Project("""<PackageReference Include="A" Version="1.0.0" VersionOverride="3.0.0" />"""));

        Assert.That(await ScanAsync(project), Is.EqualTo(new (string, string?, string?)[] { ("A", "1.0.0", null) }));
    }

    [Test]
    public async Task CheckPackageAndTransientsAsync_ReferenceMissingFromProps_ReturnsVersionNotSpecified()
    {
        Write("Directory.Packages.props", Props("""<PackageVersion Include="Other" Version="2.0.0" />"""));
        var project = Write("Project.csproj", Project("""<PackageReference Include="A" />"""));

        Assert.That(await ScanAsync(project), Is.EqualTo(new (string, string?, string?)[] { ("A", null, WarningMessages.VersionNotSpecified) }));
    }

    [Test]
    public async Task CheckPackageAndTransientsAsync_PackageIdDiffersInCase_UsesCentralVersion()
    {
        Write("Directory.Packages.props", Props("""<PackageVersion Include="a" Version="2.0.0" />"""));
        var project = Write("Project.csproj", Project("""<PackageReference Include="A" />"""));

        Assert.That(await ScanAsync(project), Is.EqualTo(new (string, string?, string?)[] { ("A", "2.0.0", null) }));
    }

    [Test]
    public async Task CheckPackageAndTransientsAsync_DuplicatePackageVersion_LastWins()
    {
        Write("Directory.Packages.props", Props("""<PackageVersion Include="A" Version="1.0.0" /><PackageVersion Include="A" Version="2.0.0" />"""));
        var project = Write("Project.csproj", Project("""<PackageReference Include="A" />"""));

        Assert.That(await ScanAsync(project), Is.EqualTo(new (string, string?, string?)[] { ("A", "2.0.0", null) }));
    }

    [Test]
    public async Task CheckPackageAndTransientsAsync_PropsWithoutFlag_UsesCentralVersion()
    {
        Write("Directory.Packages.props", Props("""<PackageVersion Include="A" Version="2.0.0" />""", properties: ""));
        var project = Write("Project.csproj", Project("""<PackageReference Include="A" />"""));

        Assert.That(await ScanAsync(project), Is.EqualTo(new (string, string?, string?)[] { ("A", "2.0.0", null) }));
    }

    [Test]
    public async Task CheckPackageAndTransientsAsync_ProjectDisablesCpm_IgnoresCentralVersionAndOverride()
    {
        Write("Directory.Packages.props", Props("""<PackageVersion Include="A" Version="2.0.0" />"""));
        var project = Write("Project.csproj", Project(
            """<PackageReference Include="A" VersionOverride="3.0.0" />""",
            "<ManagePackageVersionsCentrally>false</ManagePackageVersionsCentrally>"));

        Assert.That(await ScanAsync(project), Is.EqualTo(new (string, string?, string?)[] { ("A", null, WarningMessages.VersionNotSpecified) }));
    }

    [Test]
    public async Task CheckPackageAndTransientsAsync_PropsDisablesCpm_IgnoresCentralVersion()
    {
        Write("Directory.Packages.props", Props("""<PackageVersion Include="A" Version="2.0.0" />""",
            "<ManagePackageVersionsCentrally>false</ManagePackageVersionsCentrally>"));
        var project = Write("Project.csproj", Project("""<PackageReference Include="A" />"""));

        Assert.That(await ScanAsync(project), Is.EqualTo(new (string, string?, string?)[] { ("A", null, WarningMessages.VersionNotSpecified) }));
    }

    [Test]
    public async Task CheckPackageAndTransientsAsync_ProjectEnablesCpmOverPropsFalse_UsesCentralVersion()
    {
        Write("Directory.Packages.props", Props("""<PackageVersion Include="A" Version="2.0.0" />""",
            "<ManagePackageVersionsCentrally>false</ManagePackageVersionsCentrally>"));
        var project = Write("Project.csproj", Project(
            """<PackageReference Include="A" />""",
            "<ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>"));

        Assert.That(await ScanAsync(project), Is.EqualTo(new (string, string?, string?)[] { ("A", "2.0.0", null) }));
    }

    [Test]
    public async Task CheckPackageAndTransientsAsync_GlobalPackageReference_IsScannedAfterProjectReferences()
    {
        Write("Directory.Packages.props", Props("""<PackageVersion Include="A" Version="2.0.0" /><GlobalPackageReference Include="G" Version="1.0.0" />"""));
        var project = Write("Project.csproj", Project("""<PackageReference Include="A" />"""));

        Assert.That(await ScanAsync(project), Is.EqualTo(new (string, string?, string?)[] { ("A", "2.0.0", null), ("G", "1.0.0", null) }));
    }

    [Test]
    public async Task CheckPackageAndTransientsAsync_GlobalPackageReferenceAlsoInProject_IsScannedOnce()
    {
        Write("Directory.Packages.props", Props("""<GlobalPackageReference Include="G" Version="1.0.0" />"""));
        var project = Write("Project.csproj", Project("""<PackageReference Include="g" VersionOverride="1.0.0" />"""));

        Assert.That(await ScanAsync(project), Is.EqualTo(new (string, string?, string?)[] { ("G", "1.0.0", null) }));
    }

    [Test]
    public async Task CheckPackageAndTransientsAsync_CpmDisabled_IgnoresGlobalPackageReference()
    {
        Write("Directory.Packages.props", Props("""<GlobalPackageReference Include="G" Version="1.0.0" />""",
            "<ManagePackageVersionsCentrally>false</ManagePackageVersionsCentrally>"));
        var project = Write("Project.csproj", Project("""<PackageReference Include="A" Version="1.0.0" />"""));

        Assert.That(await ScanAsync(project), Is.EqualTo(new (string, string?, string?)[] { ("A", "1.0.0", null) }));
    }

    [Test]
    public void CheckPackageAndTransientsAsync_MalformedProps_Throws()
    {
        Write("Directory.Packages.props", "<Project><ItemGroup>");
        var project = Write("Project.csproj", Project("""<PackageReference Include="A" />"""));

        Assert.That(() => ScanAsync(project), Throws.InstanceOf<System.Xml.XmlException>());
    }
}
