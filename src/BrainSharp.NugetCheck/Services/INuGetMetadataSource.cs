using BrainSharp.NugetCheck.Dtos;

namespace BrainSharp.NugetCheck.Services;

public interface INuGetMetadataSource
{
    /// <summary>Returns every published version of a package, or an empty array when the package does not exist.</summary>
    Task<PackageMetadataRegistrationDto[]> GetPackageVersionsAsync(string packageId, CancellationToken ct = default);
}
