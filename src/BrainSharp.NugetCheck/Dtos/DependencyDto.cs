namespace BrainSharp.NugetCheck.Dtos;

public class DependencyDto
{
    public required string Id { get; set; }

    /// <summary>NuGet version range, e.g. "[4.1.0, )".</summary>
    public required string VersionRange { get; set; }
}
