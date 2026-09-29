using BrainSharp.NugetCheck.ConsoleApp;

namespace BrainSharp.NugetCheck.Tests.Unit;

public class CommandLineParserTests
{
    private static readonly string CurrentDirectory = Path.Combine(Path.GetTempPath(), "work");

    private static CliCommand Parse(string commandLine) =>
        CommandLineParser.Parse(commandLine.Split(' ', StringSplitOptions.RemoveEmptyEntries), CurrentDirectory);

    [Test]
    public void Parse_PackageWithVersion_ReturnsScanPackage()
    {
        Assert.That(Parse("package Newtonsoft.Json --version 12.0.3"), Is.EqualTo(new CliCommand.ScanPackage("Newtonsoft.Json", "12.0.3")));
    }

    [TestCase("package")]
    [TestCase("package Newtonsoft.Json")]
    [TestCase("package Newtonsoft.Json --version")]
    [TestCase("package Newtonsoft.Json -v 12.0.3")]
    public void Parse_IncompletePackageCommand_ReturnsInvalid(string commandLine)
    {
        Assert.That(Parse(commandLine), Is.InstanceOf<CliCommand.Invalid>());
    }

    [Test]
    public void Parse_Dot_ReturnsScanDirectoryForCurrentDirectory()
    {
        Assert.That(Parse("."), Is.EqualTo(new CliCommand.ScanDirectory(CurrentDirectory)));
    }

    [Test]
    public void Parse_Storage_ReturnsShowStorage()
    {
        Assert.That(Parse("storage"), Is.EqualTo(new CliCommand.ShowStorage()));
    }

    [TestCase("MyApp.csproj")]
    [TestCase("MyApp.CSPROJ")]
    public void Parse_RelativeProjectFile_ReturnsFullPath(string fileName)
    {
        var expected = Path.Combine(CurrentDirectory, "src", fileName);

        Assert.That(Parse($"src/{fileName}"), Is.EqualTo(new CliCommand.ScanProject(expected)));
    }

    [Test]
    public void Parse_AbsoluteProjectFile_KeepsPath()
    {
        var absolute = Path.Combine(Path.GetTempPath(), "other", "MyApp.csproj");

        Assert.That(CommandLineParser.Parse([absolute], CurrentDirectory), Is.EqualTo(new CliCommand.ScanProject(absolute)));
    }

    [TestCase("")]
    [TestCase("scan")]
    [TestCase(". extra")]
    [TestCase("MyApp.sln")]
    public void Parse_UnknownCommand_ReturnsInvalid(string commandLine)
    {
        Assert.That(Parse(commandLine), Is.InstanceOf<CliCommand.Invalid>());
    }

    [TestCase(0, ExitCodes.Success)]
    [TestCase(1, ExitCodes.WarningsFound)]
    [TestCase(42, ExitCodes.WarningsFound)]
    public void FromWarningCount_WarningCount_ReturnsExitCode(int warningCount, int expected)
    {
        Assert.That(ExitCodes.FromWarningCount(warningCount), Is.EqualTo(expected));
    }
}
