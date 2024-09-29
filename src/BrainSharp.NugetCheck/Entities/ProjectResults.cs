using BrainSharp.NugetCheck.Dtos;

namespace BrainSharp.NugetCheck.Entities;

public class ProjectResults
{
    public string ProjectFilePath { get; set; }
    public List<NugetPackageResults> PackageReferences { get; set; }
    public int TotalWarnings { get; set; }
}