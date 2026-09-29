using System.Xml.Linq;
using BrainSharp.NugetCheck.Dtos;
using BrainSharp.NugetCheck.Entities;
using BrainSharp.NugetCheck.Services;
using NuGet.Frameworks;
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

    /// <summary>Checks a package against every dependency group it declares.</summary>
    public Task<NugetPackageResults> CheckPackageAndTransientsAsync(string mainPackageName, string mainPackageVersion, CancellationToken ct = default) =>
        CheckRootAsync(mainPackageName, mainPackageVersion, null, ct);

    /// <summary>Checks a package as NuGet restore resolves it for <paramref name="targetFramework"/>, e.g. "net8.0".</summary>
    /// <exception cref="ArgumentException">The target framework is not recognised.</exception>
    public Task<NugetPackageResults> CheckPackageAndTransientsAsync(string mainPackageName, string mainPackageVersion, string targetFramework, CancellationToken ct = default)
    {
        var framework = ParseTargetFramework(targetFramework)
                        ?? throw new ArgumentException($"Unsupported target framework: {targetFramework}", nameof(targetFramework));
        return CheckRootAsync(mainPackageName, mainPackageVersion, [framework], ct);
    }

    /// <summary>True for a concrete target framework such as "net8.0" or "netstandard2.0".</summary>
    public static bool IsSupportedTargetFramework(string targetFramework) => ParseTargetFramework(targetFramework) != null;

    internal static NuGetFramework? ParseTargetFramework(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Contains("$(")) return null;

        try
        {
            var framework = NuGetFramework.Parse(value.Trim());
            return framework.IsSpecificFramework ? framework : null;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    // One walk per framework; a warning reached through several frameworks is reported once.
    private async Task<NugetPackageResults> CheckRootAsync(string mainPackageName, string mainPackageVersion,
        IReadOnlyList<NuGetFramework>? frameworks, CancellationToken ct)
    {
        if (frameworks is not { Count: > 0 }) return await WalkRootAsync(mainPackageName, mainPackageVersion, null, ct);

        var walks = new List<NugetPackageResults>();
        foreach (var framework in frameworks)
        {
            walks.Add(await WalkRootAsync(mainPackageName, mainPackageVersion, framework, ct));
        }

        var results = walks[0];
        results.Warnings = walks.SelectMany(walk => walk.Warnings).DistinctBy(warning => (warning.Message, warning.BreadCrumb)).ToList();
        return results;
    }

    private async Task<NugetPackageResults> WalkRootAsync(string mainPackageName, string mainPackageVersion, NuGetFramework? framework, CancellationToken ct)
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
        results.Warnings.AddRange(await GetDependencyWarningsAsync(SelectDependencies(mainPackageInfo, framework), breadCrumb, framework, ct));
        return results;
    }

    // NuGet restore uses only the nearest compatible group ("any" included); no framework means every group.
    private static IEnumerable<DependencyDto> SelectDependencies(PackageMetadataRegistrationDto packageInfo, NuGetFramework? framework)
    {
        if (framework == null) return packageInfo.DependencySets.SelectMany(group => group.Packages);

        var nearest = NuGetFrameworkUtility.GetNearest(packageInfo.DependencySets, framework, group => NuGetFramework.Parse(group.TargetFramework));
        return nearest?.Packages ?? [];
    }

    private async Task<List<Warning>> GetDependencyWarningsAsync(IEnumerable<DependencyDto> dependencies, string breadCrumb,
        NuGetFramework? framework, CancellationToken ct)
    {
        var warnings = new List<Warning>();
        foreach (var dependency in dependencies)
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
            warnings.AddRange(await GetDependencyWarningsAsync(SelectDependencies(packageInfo, framework), currentBreadCrumb, framework, ct));
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

    /// <summary>Reads every PackageReference with an Include, from any ItemGroup, ignoring XML namespaces.</summary>
    internal static List<PackageDto> ReadPackageReferences(XDocument project) =>
        project.Descendants()
            .Where(element => element.Name.LocalName == "PackageReference" && element.Parent?.Name.LocalName == "ItemGroup")
            .Select(element => new
            {
                Id = (string?)element.Attribute("Include"),
                Version = (string?)element.Attribute("Version")
                          ?? element.Elements().FirstOrDefault(child => child.Name.LocalName == "Version")?.Value
            })
            .Where(reference => !string.IsNullOrWhiteSpace(reference.Id))
            .Select(reference => new PackageDto
            {
                NugetPackageId = reference.Id!.Trim(),
                Version = string.IsNullOrWhiteSpace(reference.Version) ? null : reference.Version.Trim()
            })
            .ToList();

    /// <summary>
    /// Reads TargetFramework and TargetFrameworks from every PropertyGroup, ignoring conditions and XML namespaces.
    /// MSBuild properties such as "$(Tfm)" and unknown frameworks are skipped.
    /// </summary>
    internal static List<NuGetFramework> ReadTargetFrameworks(XDocument project) =>
        project.Descendants()
            .Where(element => element.Name.LocalName is "TargetFramework" or "TargetFrameworks"
                              && element.Parent?.Name.LocalName == "PropertyGroup")
            .SelectMany(element => element.Value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Select(ParseTargetFramework)
            .OfType<NuGetFramework>()
            .Distinct()
            .ToList();

    /// <summary>Checks every PackageReference of a project for the project's target frameworks; without one, every dependency group is checked.</summary>
    public async Task<ProjectResults> CheckPackageAndTransientsAsync(string projectFilePath, CancellationToken ct = default)
    {
        var project = XDocument.Load(projectFilePath);
        var packageReferences = ReadPackageReferences(project);
        var targetFrameworks = ReadTargetFrameworks(project);

        var packageReferencesResults = new List<NugetPackageResults>();
        foreach (var package in packageReferences)
        {
            if (package.Version == null)
            {
                packageReferencesResults.Add(new NugetPackageResults
                {
                    NugetPackageId = package.NugetPackageId,
                    Warnings = [CreateWarning(WarningMessages.VersionNotSpecified, package.NugetPackageId)]
                });
                continue;
            }

            packageReferencesResults.Add(await CheckRootAsync(package.NugetPackageId, package.Version, targetFrameworks, ct));
        }

        return new ProjectResults
        {
            ProjectFilePath = projectFilePath,
            TargetFrameworks = targetFrameworks.Select(framework => framework.GetShortFolderName()).ToList(),
            PackageReferences = packageReferencesResults,
            TotalWarnings = packageReferencesResults.Sum(package => package.Warnings.Count)
        };
    }
}
