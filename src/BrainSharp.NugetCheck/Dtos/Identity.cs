namespace BrainSharp.NugetCheck.Dtos;

public class Identity
{
    public required string Id { get; set; }

    /// <summary>Normalized version, e.g. "4.0.0".</summary>
    public required string Version { get; set; }
}
