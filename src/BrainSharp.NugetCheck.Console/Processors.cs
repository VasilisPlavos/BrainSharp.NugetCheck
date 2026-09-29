using BrainSharp.NugetCheck.Entities;

namespace BrainSharp.NugetCheck.ConsoleApp;

public static class Processors
{
    private static NugetCheck CreateNugetCheck() => new(progress: new ConsoleProgress());

    private static void DoReport(NugetPackageResults nugetPackageResults)
    {
        Console.WriteLine($"-- {nugetPackageResults.NugetPackageId} {nugetPackageResults.NugetPackageOriginalVersion} has {nugetPackageResults.Warnings.Count} warnings.");
        foreach (var warning in nugetPackageResults.Warnings)
        {
            var package = warning.BreadCrumb.Split(">").Last().Trim();
            Console.WriteLine();
            Console.WriteLine($"-- {package}: {warning.Message}");
            Console.WriteLine($"-- Breadcrumb: {warning.BreadCrumb}");

            var vulnerabilities = warning.Package?.Vulnerabilities?.ToList();
            if (vulnerabilities != null)
            {
                foreach (var vulnerability in vulnerabilities)
                {
                    Console.WriteLine($"-- Severity: {vulnerability.Severity} and AdvisoryUrl: {vulnerability.AdvisoryUrl}");
                }
            }

            if (warning.Package is { IsListed: false }) Console.WriteLine($"------ {package} is not listed!");

            var deprecations = warning.Package?.DeprecationMetadata;
            if (deprecations != null)
            {
                Console.WriteLine($"-- {package} is deprecated!");
                Console.WriteLine($"-- reason: {string.Join(",", deprecations.Reasons)}");
                Console.WriteLine($"-- message: {deprecations.Message}");
            }
        }
    }

    /// <returns>The number of warnings found.</returns>
    public static async Task<int> CheckPackageAndTransientsAsync(string packageName, string packageVersion)
    {
        var nugetPackageResults = await CreateNugetCheck().CheckPackageAndTransientsAsync(packageName, packageVersion);
        Console.WriteLine();
        DoReport(nugetPackageResults);
        return nugetPackageResults.Warnings.Count;
    }

    /// <returns>The number of warnings found in all projects.</returns>
    public static async Task<int> ScanDirectoryAsync(string directory)
    {
        Console.Write("Scanning everything...");
        var files = ProjectFinder.FindProjectFiles(directory);
        Console.WriteLine($"{files.Count} files found to scan.");

        Console.WriteLine();
        Console.WriteLine("Files to scan:");
        foreach (var file in files)
        {
            Console.WriteLine($"- {file}");
        }

        // one instance for all projects: packages are loaded once, while dedup still restarts per root package
        var nugetCheck = CreateNugetCheck();
        var totalWarnings = 0;
        foreach (var file in files)
        {
            Console.WriteLine();
            Console.WriteLine("------------------------------------------------------");
            Console.WriteLine();
            totalWarnings += await ScanProjectAsync(nugetCheck, file);
        }

        return totalWarnings;
    }

    /// <returns>The number of warnings found.</returns>
    public static Task<int> ScanProjectAsync(string filePath) => ScanProjectAsync(CreateNugetCheck(), filePath);

    private static async Task<int> ScanProjectAsync(NugetCheck nugetCheck, string filePath)
    {
        Console.WriteLine($"Scanning {filePath}");
        var result = await nugetCheck.CheckPackageAndTransientsAsync(filePath);
        Console.WriteLine();
        Console.WriteLine($"File scanned. Found {result.TotalWarnings} warnings.");
        foreach (var nugetPackageResult in result.PackageReferences)
        {
            if (!nugetPackageResult.Warnings.Any()) continue;
            Console.WriteLine();
            Console.WriteLine("------------------------------------------------------");
            Console.WriteLine();
            DoReport(nugetPackageResult);
        }

        return result.TotalWarnings;
    }
}
