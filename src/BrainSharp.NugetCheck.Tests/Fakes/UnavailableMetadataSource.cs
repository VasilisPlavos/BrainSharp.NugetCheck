using BrainSharp.NugetCheck.Dtos;
using BrainSharp.NugetCheck.Services;

namespace BrainSharp.NugetCheck.Tests.Fakes;

/// <summary>Behaves like nuget.org during an outage.</summary>
public class UnavailableMetadataSource : INuGetMetadataSource
{
    public Task<PackageMetadataRegistrationDto[]> GetPackageVersionsAsync(string packageId, CancellationToken ct = default) =>
        throw new HttpRequestException("nuget.org is unreachable");
}
