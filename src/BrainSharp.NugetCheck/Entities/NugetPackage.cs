using BrainSharp.NugetCheck.Dtos;

namespace BrainSharp.NugetCheck.Entities;

public class NugetPackage
{
    public DateTime DateScanned { get; set; }
    public required string NugetPackageId { get; set; }
    public PackageMetadataRegistrationDto[] PackageMetadataRegistrations { get; set; }
}