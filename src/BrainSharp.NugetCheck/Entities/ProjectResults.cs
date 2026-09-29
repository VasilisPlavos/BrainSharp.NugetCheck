namespace BrainSharp.NugetCheck.Entities;

public class ProjectResults
{
    public required string ProjectFilePath { get; set; }
    public List<NugetPackageResults> PackageReferences { get; set; } = [];
    public int TotalWarnings { get; set; }
}
