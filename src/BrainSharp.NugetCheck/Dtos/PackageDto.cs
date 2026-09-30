namespace BrainSharp.NugetCheck.Dtos;

public class PackageDto
{
    public required string NugetPackageId { get; set; }

    /// <summary>Null when neither the project nor its Directory.Packages.props specifies one.</summary>
    public string? Version { get; set; }
}
