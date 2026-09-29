using BrainSharp.NugetCheck.ConsoleApp;
using BrainSharp.NugetCheck.Services;
using BrainSharp.NugetCheck.Tests.Fakes;

namespace BrainSharp.NugetCheck.Tests.Unit;

public class ProgramTests
{
    private string _directory = null!;

    [SetUp]
    public void SetUp() => _directory = TestDirectories.Create();

    [TearDown]
    public void TearDown() => TestDirectories.Delete(_directory);

    private void WriteFile(string relativePath, string content)
    {
        var path = Path.Combine(_directory, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    private static NugetCheck CreateNugetCheck(INuGetMetadataSource source) => new(source, new InMemoryPackageCache());

    private const string ProjectReferencingA = """
        <Project Sdk="Microsoft.NET.Sdk">
          <ItemGroup>
            <PackageReference Include="A" Version="1.0.0" />
          </ItemGroup>
        </Project>
        """;

    [Test]
    public async Task RunAsync_DirectoryWithVulnerableProject_ReturnsWarningsFound()
    {
        var source = new FakeMetadataSource().WithPackage("A", "1.0.0", vulnerable: true);
        WriteFile(Path.Combine("Good", "Good.csproj"), ProjectReferencingA);

        var exitCode = await Program.RunAsync(["."], _directory, CreateNugetCheck(source));

        Assert.That(exitCode, Is.EqualTo(ExitCodes.WarningsFound));
    }

    [Test]
    public async Task RunAsync_DirectoryWithMalformedProject_ScansTheOthersAndReturnsScanFailed()
    {
        var source = new FakeMetadataSource().WithPackage("A", "1.0.0");
        WriteFile(Path.Combine("Broken", "Broken.csproj"), "<Project");
        WriteFile(Path.Combine("Good", "Good.csproj"), ProjectReferencingA);

        var exitCode = await Program.RunAsync(["."], _directory, CreateNugetCheck(source));

        Assert.That(exitCode, Is.EqualTo(ExitCodes.ScanFailed));
        Assert.That(source.CallsFor("A"), Is.EqualTo(1));
    }

    [Test]
    public async Task RunAsync_NuGetUnavailable_ReturnsScanFailed()
    {
        var exitCode = await Program.RunAsync(["package", "A", "--version", "1.0.0"], _directory,
            CreateNugetCheck(new UnavailableMetadataSource()));

        Assert.That(exitCode, Is.EqualTo(ExitCodes.ScanFailed));
    }

    [Test]
    public async Task RunAsync_MalformedProjectFile_ReturnsScanFailed()
    {
        WriteFile("Broken.csproj", "<Project");

        var exitCode = await Program.RunAsync(["Broken.csproj"], _directory, CreateNugetCheck(new FakeMetadataSource()));

        Assert.That(exitCode, Is.EqualTo(ExitCodes.ScanFailed));
    }
}
