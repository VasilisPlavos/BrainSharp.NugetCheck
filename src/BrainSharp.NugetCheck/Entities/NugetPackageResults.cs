namespace BrainSharp.NugetCheck.Entities;

public class NugetPackageResults
{
    public required string NugetPackageId { get; set; }
    public string? NugetPackageOriginalVersion { get; set; }
    public List<Warning> Warnings { get; set; } = [];
}
