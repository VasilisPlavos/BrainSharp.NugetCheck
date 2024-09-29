namespace BrainSharp.NugetCheck.ConsoleApp;

class Program
{
    static async Task Main(string[] args)
    {
        if (!args.Any())
        {
            Console.WriteLine("Wrong command!");
            return;
        }

        if (args[0] == ".")
        {
            await Processors.ScanEverythingAsync(Directory.GetCurrentDirectory());
            return;
        }

        if (args[0] == "storage")
        {
            Console.WriteLine(Path.Combine(AppContext.BaseDirectory, "storage"));
            return;
        }

        if (args[0].EndsWith(".csproj"))
        {
            var filePath = File.Exists(args[0]) ? args[0] : Path.Combine(Directory.GetCurrentDirectory(), args[0]);
            await Processors.ScanProjectAsync(filePath);
            return;
        }

        if (args[0] == "package" && args[2] == "--version")
        {
            var packageName = args[1];
            var packageVersion = args[3];
            Console.WriteLine($"Scanning package {packageName} with version {packageVersion}");
            await Processors.CheckPackageAndTransientsAsync(packageName, packageVersion);
            return;
        }

        Console.WriteLine("Wrong command!");
    }
}