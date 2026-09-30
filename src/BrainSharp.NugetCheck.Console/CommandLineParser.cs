namespace BrainSharp.NugetCheck.ConsoleApp;

public static class CommandLineParser
{
    public const string Usage = """
        Usage:
          nugetscan package <PackageId> --version <Version> [--framework <TFM>]
                                                              Check a package and its transitive dependencies
                                                              (only the dependencies used by <TFM>, e.g. net8.0)
          nugetscan <path/to/Project.csproj>                  Check every PackageReference of a project
          nugetscan .                                         Check every project below the current folder
          nugetscan storage                                   Show where the local cache is stored

        Exit codes: 0 = no warnings, 1 = warnings found, 2 = invalid usage, 3 = scan could not run
        """;

    public static CliCommand Parse(string[] args, string currentDirectory) => args switch
    {
        [] => new CliCommand.Invalid("No command given."),
        ["."] => new CliCommand.ScanDirectory(currentDirectory),
        ["storage"] => new CliCommand.ShowStorage(),
        ["package", var packageId, "--version", var version] => new CliCommand.ScanPackage(packageId, version),
        ["package", var packageId, "--version", var version, "--framework", var framework] => ScanPackage(packageId, version, framework),
        ["package", var packageId, "--framework", var framework, "--version", var version] => ScanPackage(packageId, version, framework),
        ["package", ..] => new CliCommand.Invalid("Expected: package <PackageId> --version <Version> [--framework <TFM>]"),
        [var path] when path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)
            => new CliCommand.ScanProject(Path.GetFullPath(path, currentDirectory)),
        _ => new CliCommand.Invalid($"Unknown command: {string.Join(' ', args)}")
    };

    private static CliCommand ScanPackage(string packageId, string version, string framework) =>
        NugetCheck.IsSupportedTargetFramework(framework)
            ? new CliCommand.ScanPackage(packageId, version, framework)
            : new CliCommand.Invalid($"Unknown target framework: {framework}");
}
