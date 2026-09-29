namespace BrainSharp.NugetCheck.Dtos;

public class PackageDto
{
    public required string NugetPackageId { get; set; }

    /// <summary>Null when the project does not specify one (e.g. Central Package Management).</summary>
    public string? Version { get; set; }
}
