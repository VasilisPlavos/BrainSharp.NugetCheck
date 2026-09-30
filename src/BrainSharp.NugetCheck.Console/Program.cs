using BrainSharp.NugetCheck.Services;

namespace BrainSharp.NugetCheck.ConsoleApp;

class Program
{
    static Task<int> Main(string[] args) =>
        RunAsync(args, Directory.GetCurrentDirectory(), new NugetCheck(progress: new ConsoleProgress()));

    internal static async Task<int> RunAsync(string[] args, string currentDirectory, NugetCheck nugetCheck)
    {
        try
        {
            switch (CommandLineParser.Parse(args, currentDirectory))
            {
                case CliCommand.ShowStorage:
                    Console.WriteLine(FilePackageCache.DefaultLocation);
                    return ExitCodes.Success;

                case CliCommand.ScanDirectory command:
                    var (warningCount, failedCount) = await Processors.ScanDirectoryAsync(nugetCheck, command.DirectoryPath);
                    return ExitCodes.FromScan(warningCount, failedCount);

                case CliCommand.ScanProject command when File.Exists(command.ProjectFilePath):
                    return ExitCodes.FromWarningCount(await Processors.ScanProjectAsync(nugetCheck, command.ProjectFilePath));

                case CliCommand.ScanProject command:
                    return PrintUsage($"File not found: {command.ProjectFilePath}");

                case CliCommand.ScanPackage command:
                    var forFramework = command.Framework == null ? "" : $" for {command.Framework}";
                    Console.WriteLine($"Scanning package {command.PackageId} with version {command.Version}{forFramework}");
                    return ExitCodes.FromWarningCount(
                        await Processors.CheckPackageAndTransientsAsync(nugetCheck, command.PackageId, command.Version, command.Framework));

                case CliCommand.Invalid command:
                    return PrintUsage(command.Reason);

                default:
                    return PrintUsage("Unknown command.");
            }
        }
        catch (Exception e)
        {
            // e.g. nuget.org unreachable or a malformed project file: a readable message instead of a stack trace
            Console.Error.WriteLine();
            Console.Error.WriteLine($"The scan could not run: {e.Message}");
            return ExitCodes.ScanFailed;
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
