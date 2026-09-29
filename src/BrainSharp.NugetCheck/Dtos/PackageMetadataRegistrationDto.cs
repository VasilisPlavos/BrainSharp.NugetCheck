using NuGet.Protocol;

namespace BrainSharp.NugetCheck.Dtos;

public class PackageMetadataRegistrationDto
{
    public required Identity Identity { get; set; }
    public required string OriginalVersion { get; set; }

    // Our own DTOs instead of PackageDependencyGroup / PackageDeprecationMetadata: the VersionRange and NuGetFramework they contain cannot be read back from the JSON cache.
    public List<DependencyGroupDto> DependencySets { get; set; } = [];
    public DeprecationDto? DeprecationMetadata { get; set; }
    public bool IsListed { get; set; }
    public IEnumerable<PackageVulnerabilityMetadata>? Vulnerabilities { get; set; }
}
