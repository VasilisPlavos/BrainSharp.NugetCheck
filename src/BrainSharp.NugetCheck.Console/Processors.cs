using BrainSharp.NugetCheck.Entities;

namespace BrainSharp.NugetCheck.ConsoleApp;

public static class Processors
{
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
    public static async Task<int> CheckPackageAndTransientsAsync(NugetCheck nugetCheck, string packageName, string packageVersion, string? framework)
    {
        var nugetPackageResults = framework == null
            ? await nugetCheck.CheckPackageAndTransientsAsync(packageName, packageVersion)
            : await nugetCheck.CheckPackageAndTransientsAsync(packageName, packageVersion, framework);
        Console.WriteLine();
        DoReport(nugetPackageResults);
        return nugetPackageResults.Warnings.Count;
    }

    /// <summary>Scans every project below <paramref name="directory"/>; a project that cannot be scanned does not stop the others.</summary>
    /// <remarks>Uses one <see cref="NugetCheck"/> for all projects: packages are loaded once, while dedup still restarts per root package.</remarks>
    public static async Task<(int WarningCount, int FailedCount)> ScanDirectoryAsync(NugetCheck nugetCheck, string directory)
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

        var warningCount = 0;
        var failedCount = 0;
        foreach (var file in files)
        {
            Console.WriteLine();
            Console.WriteLine("------------------------------------------------------");
            Console.WriteLine();
            try
            {
                warningCount += await ScanProjectAsync(nugetCheck, file);
            }
            catch (Exception e)
            {
                failedCount++;
                Console.Error.WriteLine();
                Console.Error.WriteLine($"Could not scan {file}: {e.Message}");
            }
        }

        if (failedCount > 0)
        {
            Console.Error.WriteLine();
            Console.Error.WriteLine($"{failedCount} of {files.Count} projects could not be scanned.");
        }

        return (warningCount, failedCount);
    }

    /// <returns>The number of warnings found.</returns>
    public static async Task<int> ScanProjectAsync(NugetCheck nugetCheck, string filePath)
    {
        Console.WriteLine($"Scanning {filePath}");
        var result = await nugetCheck.CheckPackageAndTransientsAsync(filePath);
        Console.WriteLine(result.TargetFrameworks.Count > 0
            ? $"Target frameworks: {string.Join(", ", result.TargetFrameworks)}"
            : "Target framework not found; checking all dependency groups");
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
