using BrainSharp.NugetCheck.ConsoleApp;
using BrainSharp.NugetCheck.Tests.Fakes;

namespace BrainSharp.NugetCheck.Tests.Unit;

public class ProjectFinderTests
{
    private string _directory = null!;

    [SetUp]
    public void SetUp() => _directory = TestDirectories.Create();

    [TearDown]
    public void TearDown() => TestDirectories.Delete(_directory);

    private string CreateFile(params string[] pathParts)
    {
        var path = Path.Combine([_directory, .. pathParts]);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "<Project />");
        return path;
    }

    [Test]
    public void FindProjectFiles_BuildOutputAndNodeModules_AreSkipped()
    {
        var app = CreateFile("App", "App.csproj");
        var nested = CreateFile("libs", "Nested", "Lib.csproj");
        CreateFile("App", "bin", "Debug", "net10.0", "Files", "Copied.csproj");
        CreateFile("App", "obj", "Generated.csproj");
        CreateFile("node_modules", "pkg", "Package.csproj");
        CreateFile("App", "README.md");

        var files = ProjectFinder.FindProjectFiles(_directory);

        Assert.That(files, Is.EqualTo(new[] { app, nested }));
    }

    [Test]
    public void FindProjectFiles_EmptyDirectory_ReturnsEmpty()
    {
        Assert.That(ProjectFinder.FindProjectFiles(_directory), Is.Empty);
    }
}
