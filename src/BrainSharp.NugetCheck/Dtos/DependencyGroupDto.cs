namespace BrainSharp.NugetCheck.Dtos;

public class DependencyGroupDto
{
    public required string TargetFramework { get; set; }
    public List<DependencyDto> Packages { get; set; } = [];
}
