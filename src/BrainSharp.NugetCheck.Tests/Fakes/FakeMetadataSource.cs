using BrainSharp.NugetCheck.Dtos;
using BrainSharp.NugetCheck.Services;
using Newtonsoft.Json;
using NuGet.Protocol;
using NuGet.Versioning;

namespace BrainSharp.NugetCheck.Tests.Fakes;

/// <summary>In-memory stand-in for nuget.org. Dependencies are written as "Id VersionRange", e.g. "B [1.0.0, )".</summary>
public class FakeMetadataSource : INuGetMetadataSource
{
    private readonly Dictionary<string, List<PackageMetadataRegistrationDto>> _packages = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _calls = new(StringComparer.OrdinalIgnoreCase);

    public FakeMetadataSource WithPackage(string id, string version, string[]? dependencies = null,
        bool vulnerable = false, bool deprecated = false, bool listed = true)
    {
        var registration = new PackageMetadataRegistrationDto
        {
            Identity = new Identity { Id = id, Version = NuGetVersion.Parse(version).ToNormalizedString() },
            OriginalVersion = version,
            IsListed = listed,
            Vulnerabilities = vulnerable ? [CreateVulnerability()] : null,
            DeprecationMetadata = deprecated ? CreateDeprecation() : null
        };

        if (!_packages.TryGetValue(id, out var versions)) _packages[id] = versions = [];
        versions.Add(registration);

        return dependencies == null ? this : WithDependencyGroup(id, version, "net8.0", dependencies);
    }

    public FakeMetadataSource WithDependencyGroup(string id, string version, string targetFramework, params string[] dependencies)
    {
        var normalizedVersion = NuGetVersion.Parse(version).ToNormalizedString();
        var registration = _packages[id].Single(x => x.Identity.Version == normalizedVersion);
        registration.DependencySets.Add(new DependencyGroupDto
        {
            TargetFramework = targetFramework,
            Packages = dependencies.Select(ParseDependency).ToList()
        });
        return this;
    }

    public PackageMetadataRegistrationDto[] VersionsOf(string id) => _packages.TryGetValue(id, out var versions) ? versions.ToArray() : [];

    public int CallsFor(string id) => _calls.GetValueOrDefault(id);

    public Task<PackageMetadataRegistrationDto[]> GetPackageVersionsAsync(string packageId, CancellationToken ct = default)
    {
        _calls[packageId] = CallsFor(packageId) + 1;
        return Task.FromResult(VersionsOf(packageId));
    }

    private static DependencyDto ParseDependency(string dependency)
    {
        var parts = dependency.Split(' ', 2);
        return new DependencyDto { Id = parts[0], VersionRange = parts[1] };
    }

    // Created from JSON, the same way NuGet.Protocol creates it from the registration API.
    private static PackageVulnerabilityMetadata CreateVulnerability() =>
        JsonConvert.DeserializeObject<PackageVulnerabilityMetadata>("""{"advisoryUrl":"https://example.com/advisory","severity":2}""")!;

    private static DeprecationDto CreateDeprecation() => new()
    {
        Message = "Use something else",
        Reasons = ["Legacy"],
        AlternatePackageId = "B",
        AlternatePackageVersionRange = "[2.0.0, )"
    };
}
