using BrainSharp.NugetCheck.Dtos;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;

namespace BrainSharp.NugetCheck.Services;

public sealed class NuGetOrgMetadataSource : INuGetMetadataSource
{
    private const string FeedUrl = "https://api.nuget.org/v3/index.json";

    private readonly SourceRepository _repository = Repository.Factory.GetCoreV3(new PackageSource(FeedUrl));
    private PackageMetadataResource? _resource;

    public async Task<PackageMetadataRegistrationDto[]> GetPackageVersionsAsync(string packageId, CancellationToken ct = default)
    {
        _resource ??= await _repository.GetResourceAsync<PackageMetadataResource>(ct)
                      ?? throw new InvalidOperationException($"{FeedUrl} does not provide package metadata.");

        using var cacheContext = new SourceCacheContext();
        var metadata = await _resource.GetMetadataAsync(packageId, includePrerelease: true, includeUnlisted: true, cacheContext, NullLogger.Instance, ct);

        return (metadata ?? [])
            .OfType<PackageSearchMetadataRegistration>()
            .Where(x => string.Equals(x.Identity.Id, packageId, StringComparison.OrdinalIgnoreCase))
            .Select(ToDto)
            .ToArray();
    }

    private static PackageMetadataRegistrationDto ToDto(PackageSearchMetadataRegistration metadata) => new()
    {
        Identity = new Identity { Id = metadata.Identity.Id, Version = metadata.Identity.Version.ToNormalizedString() },
        OriginalVersion = metadata.Version.OriginalVersion ?? metadata.Version.ToNormalizedString(),
        DependencySets = metadata.DependencySets
            .Select(group => new DependencyGroupDto
            {
                TargetFramework = group.TargetFramework.GetShortFolderName(),
                Packages = group.Packages
                    .Select(package => new DependencyDto { Id = package.Id, VersionRange = package.VersionRange.ToNormalizedString() })
                    .ToList()
            })
            .ToList(),
        DeprecationMetadata = metadata.DeprecationMetadata == null ? null : new DeprecationDto
        {
            Message = metadata.DeprecationMetadata.Message,
            Reasons = metadata.DeprecationMetadata.Reasons?.ToList() ?? [],
            AlternatePackageId = metadata.DeprecationMetadata.AlternatePackage?.PackageId,
            AlternatePackageVersionRange = metadata.DeprecationMetadata.AlternatePackage?.Range?.ToNormalizedString()
        },
        IsListed = metadata.IsListed,
        Vulnerabilities = metadata.Vulnerabilities?.ToList()
    };
}
