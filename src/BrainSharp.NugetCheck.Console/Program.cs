using BrainSharp.NugetCheck.Services;

namespace BrainSharp.NugetCheck.ConsoleApp;

class Program
{
    static async Task<int> Main(string[] args)
    {
        switch (CommandLineParser.Parse(args, Directory.GetCurrentDirectory()))
        {
            case CliCommand.ShowStorage:
                Console.WriteLine(FilePackageCache.DefaultLocation);
                return ExitCodes.Success;

            case CliCommand.ScanDirectory command:
                return ExitCodes.FromWarningCount(await Processors.ScanDirectoryAsync(command.DirectoryPath));

            case CliCommand.ScanProject command when File.Exists(command.ProjectFilePath):
                return ExitCodes.FromWarningCount(await Processors.ScanProjectAsync(command.ProjectFilePath));

            case CliCommand.ScanProject command:
                return PrintUsage($"File not found: {command.ProjectFilePath}");

            case CliCommand.ScanPackage command:
                Console.WriteLine($"Scanning package {command.PackageId} with version {command.Version}");
                return ExitCodes.FromWarningCount(await Processors.CheckPackageAndTransientsAsync(command.PackageId, command.Version));

            case CliCommand.Invalid command:
                return PrintUsage(command.Reason);

            default:
                return PrintUsage("Unknown command.");
        }
    }

    private static int PrintUsage(string reason)
    {
        Console.Error.WriteLine(reason);
        Console.Error.WriteLine();
        Console.Error.WriteLine(CommandLineParser.Usage);
        return ExitCodes.InvalidUsage;
    }
}
