using NuGet.Packaging;
using NuGet.Protocol;
using static BrainSharp.NugetCheck.Entities.NugetPackage2;

namespace BrainSharp.NugetCheck.Dtos;

public class PackageMetadataRegistrationDto
{
    public Identity Identity { get; set; }
    public string OriginalVersion { get; set; }
    public IEnumerable<PackageDependencyGroup> DependencySets { get; set; }
    public PackageDeprecationMetadata DeprecationMetadata { get; set; }
    public bool IsListed { get; set; }
    public IEnumerable<PackageVulnerabilityMetadata>? Vulnerabilities { get; set; }
}