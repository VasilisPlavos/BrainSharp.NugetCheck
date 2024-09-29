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

    public static async Task CheckPackageAndTransientsAsync(string packageName, string packageVersion)
    {
        var nugetCheck = new NugetCheck();
        var nugetPackageResults = await nugetCheck.CheckPackageAndTransientsAsync(packageName, packageVersion);
        DoReport(nugetPackageResults);
    }

    public static async Task ScanEverythingAsync(string currentDirectory)
    {
        Console.Write("Scanning everything...");
        var files = Directory.GetFiles(Directory.GetCurrentDirectory(), "*.csproj", SearchOption.AllDirectories);
        Console.WriteLine($"{files.Length} files found to scan.");

        Console.WriteLine();
        Console.WriteLine("Files to scan:");
        foreach (var file in files)
        {
            Console.WriteLine($"- {file}");
        }

        foreach (var file in files)
        {
            Console.WriteLine();
            Console.WriteLine("------------------------------------------------------");
            Console.WriteLine();
            var filePath = File.Exists(file) ? file : Path.Combine(Directory.GetCurrentDirectory(), file);
            await ScanProjectAsync(filePath);
        }
    }

    public static async Task ScanProjectAsync(string filePath)
    {
        Console.WriteLine($"Scanning {filePath}");
        var nugetCheck = new NugetCheck();
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
    }
}