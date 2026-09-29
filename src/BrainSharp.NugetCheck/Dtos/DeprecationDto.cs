namespace BrainSharp.NugetCheck.Dtos;

public class DeprecationDto
{
    public string? Message { get; set; }
    public List<string> Reasons { get; set; } = [];
    public string? AlternatePackageId { get; set; }

    /// <summary>NuGet version range of the suggested alternative, e.g. "[2.0.0, )".</summary>
    public string? AlternatePackageVersionRange { get; set; }
}
