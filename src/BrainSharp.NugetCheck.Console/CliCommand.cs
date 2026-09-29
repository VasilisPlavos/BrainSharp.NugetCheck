namespace BrainSharp.NugetCheck.ConsoleApp;

public abstract record CliCommand
{
    public sealed record ScanPackage(string PackageId, string Version) : CliCommand;
    public sealed record ScanProject(string ProjectFilePath) : CliCommand;
    public sealed record ScanDirectory(string DirectoryPath) : CliCommand;
    public sealed record ShowStorage : CliCommand;
    public sealed record Invalid(string Reason) : CliCommand;
}
