namespace BrainSharp.NugetCheck.Entities;

public class ProjectResults
{
    public required string ProjectFilePath { get; set; }

    /// <summary>Target frameworks the scan used, e.g. "net8.0"; empty when none was found and every dependency group was checked.</summary>
    public List<string> TargetFrameworks { get; set; } = [];

    public List<NugetPackageResults> PackageReferences { get; set; } = [];
    public int TotalWarnings { get; set; }
}
