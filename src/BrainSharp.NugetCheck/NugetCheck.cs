using System.Xml.Linq;
using BrainSharp.NugetCheck.Dtos;
using BrainSharp.NugetCheck.Entities;
using BrainSharp.NugetCheck.Services;
using NuGet.Versioning;

namespace BrainSharp.NugetCheck;

/// <summary>
/// Checks NuGet packages and their transitive dependencies for vulnerable, deprecated and unlisted versions.
/// Not thread-safe: use one instance per concurrent scan.
/// </summary>
public class NugetCheck
{
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromDays(1);

    private readonly INuGetMetadataSource _source;
    private readonly IPackageCache _cache;
    private readonly IProgress<string>? _progress;
    private readonly Dictionary<string, NugetPackage?> _packages = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _scannedPackages = new(StringComparer.OrdinalIgnoreCase);

    public NugetCheck(INuGetMetadataSource? source = null, IPackageCache? cache = null, IProgress<string>? progress = null)
    {
        _source = source ?? new NuGetOrgMetadataSource();
        _cache = cache ?? new FilePackageCache();
        _progress = progress;
    }

    public async Task<NugetPackageResults> CheckPackageAndTransientsAsync(string mainPackageName, string mainPackageVersion, CancellationToken ct = default)
    {
        // every root package gets its own walk, so project results do not depend on reference order
        _scannedPackages.Clear();
        var results = new NugetPackageResults { NugetPackageId = mainPackageName };

        var mainPackage = await SearchPackageAsync(mainPackageName, ct);
        if (mainPackage == null)
        {
            results.Warnings.Add(CreateWarning(WarningMessages.PackageNotFound, $"{mainPackageName} {mainPackageVersion}"));
            return results;
        }

        results.NugetPackageId = mainPackage.NugetPackageId;
        var mainPackageInfo = ResolveRootVersion(mainPackage, mainPackageVersion);
        if (mainPackageInfo == null)
        {
            results.Warnings.Add(CreateWarning(WarningMessages.VersionNotFound, $"{mainPackage.NugetPackageId} {mainPackageVersion}"));
            return results;
        }

        results.NugetPackageOriginalVersion = mainPackageInfo.OriginalVersion;
        MarkAsScanned(mainPackageInfo);

        var breadCrumb = $"{mainPackage.NugetPackageId} {mainPackageInfo.OriginalVersion}";
        results.Warnings.AddRange(GetPackageWarnings(mainPackageInfo, breadCrumb));
        results.Warnings.AddRange(await GetDependencyWarningsAsync(mainPackageInfo.DependencySets, breadCrumb, ct));
        return results;
    }

    private async Task<List<Warning>> GetDependencyWarningsAsync(IEnumerable<DependencyGroupDto> dependencyGroups, string breadCrumb, CancellationToken ct)
    {
        var warnings = new List<Warning>();
        foreach (var dependency in dependencyGroups.SelectMany(group => group.Packages))
        {
            var requestedBreadCrumb = GetCurrentBreadCrumb(breadCrumb, dependency.Id, dependency.VersionRange);

            var package = await SearchPackageAsync(dependency.Id, ct);
            if (package == null)
            {
                if (_scannedPackages.Add(ScanKey(dependency.Id, dependency.VersionRange)))
                    warnings.Add(CreateWarning(WarningMessages.PackageNotFound, requestedBreadCrumb));
                continue;
            }

            var packageInfo = ResolveDependencyVersion(package, dependency.VersionRange);
            if (packageInfo == null)
            {
                if (_scannedPackages.Add(ScanKey(package.NugetPackageId, dependency.VersionRange)))
                    warnings.Add(CreateWarning(WarningMessages.VersionNotFound, requestedBreadCrumb));
                continue;
            }

            // already walked in this tree (shared dependency or cycle)
            if (!MarkAsScanned(packageInfo)) continue;

            var currentBreadCrumb = GetCurrentBreadCrumb(breadCrumb, package.NugetPackageId, packageInfo.OriginalVersion);
            warnings.AddRange(GetPackageWarnings(packageInfo, currentBreadCrumb));
            warnings.AddRange(await GetDependencyWarningsAsync(packageInfo.DependencySets, currentBreadCrumb, ct));
        }

        return warnings;
    }

    private bool MarkAsScanned(PackageMetadataRegistrationDto packageInfo)
    {
        if (!_scannedPackages.Add(ScanKey(packageInfo.Identity.Id, packageInfo.Identity.Version))) return false;

        _progress?.Report($"Scanning {packageInfo.Identity.Id} {packageInfo.OriginalVersion}");
        return true;
    }

    private static string ScanKey(string packageId, string version) => $"{packageId}@{version}";

    private static string GetCurrentBreadCrumb(string breadCrumb, string packageId, string packageVersion)
    {
        return $"{breadCrumb} > {packageId} {packageVersion}";
    }

    // An exact version must exist as-is; pins, ranges and floating versions ("[1.0.0]", "1.*") resolve like NuGet restore.
    private PackageMetadataRegistrationDto? ResolveRootVersion(NugetPackage package, string version) =>
        NuGetVersion.TryParse(version, out _) ? SearchPackageVersionInfo(package, version) : ResolveDependencyVersion(package, version);

    // NuGet restore picks the lowest version that satisfies the range.
    private static PackageMetadataRegistrationDto? ResolveDependencyVersion(NugetPackage package, string versionRange)
    {
        if (!VersionRange.TryParse(versionRange, out var range)) return null;

        var candidates = package.PackageMetadataRegistrations
            .Select(info => (Version: NuGetVersion.Parse(info.Identity.Version), Info: info))
            .ToList();

        var bestMatch = range.FindBestMatch(candidates.Select(candidate => candidate.Version));
        return bestMatch == null ? null : candidates.First(candidate => candidate.Version == bestMatch).Info;
    }

    public async Task<bool?> IsDeprecatedAsync(string packageName, string packageVersion, CancellationToken ct = default)
    {
        var packageInfo = await FindPackageVersionAsync(packageName, packageVersion, ct);
        return packageInfo == null ? null : packageInfo.DeprecationMetadata != null;
    }

    public async Task<bool?> IsListedAsync(string packageName, string packageVersion, CancellationToken ct = default)
    {
        var packageInfo = await FindPackageVersionAsync(packageName, packageVersion, ct);
        return packageInfo?.IsListed;
    }

    public async Task<bool?> IsVulnerableAsync(string packageName, string packageVersion, CancellationToken ct = default)
    {
        var packageInfo = await FindPackageVersionAsync(packageName, packageVersion, ct);
        return packageInfo == null ? null : IsVulnerable(packageInfo);
    }

    private async Task<PackageMetadataRegistrationDto?> FindPackageVersionAsync(string packageName, string packageVersion, CancellationToken ct)
    {
        var package = await SearchPackageAsync(packageName, ct);
        return package == null ? null : SearchPackageVersionInfo(package, packageVersion);
    }

    private static bool IsVulnerable(PackageMetadataRegistrationDto packageInfo) => packageInfo.Vulnerabilities?.Any() == true;

    private static List<Warning> GetPackageWarnings(PackageMetadataRegistrationDto packageInfo, string breadCrumb)
    {
        var warnings = new List<Warning>();
        if (!packageInfo.IsListed) warnings.Add(CreateWarning(WarningMessages.NotListed, breadCrumb, packageInfo));
        if (IsVulnerable(packageInfo)) warnings.Add(CreateWarning(WarningMessages.Vulnerable, breadCrumb, packageInfo));
        if (packageInfo.DeprecationMetadata != null) warnings.Add(CreateWarning(WarningMessages.Deprecated, breadCrumb, packageInfo));
        return warnings;
    }

    private static Warning CreateWarning(string message, string breadCrumb, PackageMetadataRegistrationDto? package = null) =>
        new() { Message = message, BreadCrumb = breadCrumb, Package = package };

    public async Task<NugetPackage?> SearchPackageAsync(string packageName, CancellationToken ct = default)
    {
        if (_packages.TryGetValue(packageName, out var known)) return known;

        var package = await LoadPackageAsync(packageName, ct);
        _packages[packageName] = package;
        return package;
    }

    private async Task<NugetPackage?> LoadPackageAsync(string packageName, CancellationToken ct)
    {
        var cached = await _cache.GetAsync(packageName, ct);
        if (cached != null && cached.DateScanned > DateTime.UtcNow - CacheLifetime) return cached;

        var versions = await _source.GetPackageVersionsAsync(packageName, ct);
        if (versions.Length == 0) return null;

        var package = new NugetPackage
        {
            NugetPackageId = versions[0].Identity.Id,
            DateScanned = DateTime.UtcNow,
            PackageMetadataRegistrations = versions
        };

        await _cache.SaveAsync(package, ct);
        return package;
    }

    /// <summary>Finds an exact version; "4.0" matches "4.0.0".</summary>
    public PackageMetadataRegistrationDto? SearchPackageVersionInfo(NugetPackage package, string packageVersion)
    {
        if (!NuGetVersion.TryParse(packageVersion, out var version)) return null;
        return package.PackageMetadataRegistrations.FirstOrDefault(info => NuGetVersion.Parse(info.Identity.Version) == version);
    }

    private static List<PackageDto> GetProjectPackageList(string filePath) => GetProjectPackageList(XDocument.Load(filePath));
    private static List<PackageDto> GetProjectPackageList(XDocument csProjToXDocument)
    {
        var listOfPackages = new List<PackageDto>();
        var itemGroups = csProjToXDocument.Elements().ToList().Elements().ToList().Where(x => x.Name == "ItemGroup").ToList();
        foreach (var itemGroup in itemGroups)
        {
            foreach (var item in itemGroup.Elements().Where(x => x.Name == "PackageReference").ToList())
            {
                var inc = item.Attributes().ToList();
                var version = inc.Where(x => x.Name == "Version").Select(x => x.Value).FirstOrDefault();
                var packageName = inc.Where(x => x.Name == "Include").Select(x => x.Value).FirstOrDefault();
                listOfPackages.Add(new PackageDto
                {
                    Version = version!,
                    NugetPackageId = packageName!
                });
            }
        }

        return listOfPackages;
    }

    public async Task<ProjectResults> CheckPackageAndTransientsAsync(string projectFilePath, CancellationToken ct = default)
    {
        var packageReferences = GetProjectPackageList(projectFilePath);

        var packageReferencesResults = new List<NugetPackageResults>();
        foreach (var package in packageReferences)
        {
            var packageResults = await CheckPackageAndTransientsAsync(package.NugetPackageId, package.Version, ct);
            packageReferencesResults.Add(packageResults);
        }

        return new ProjectResults
        {
            ProjectFilePath = projectFilePath,
            PackageReferences = packageReferencesResults,
            TotalWarnings = packageReferencesResults.Sum(package => package.Warnings.Count)
        };
    }
}
