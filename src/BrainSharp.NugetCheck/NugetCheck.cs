using System.Xml.Linq;
using BrainSharp.NugetCheck.Dtos;
using BrainSharp.NugetCheck.Entities;
using BrainSharp.NugetCheck.Services;

namespace BrainSharp.NugetCheck;

public class NugetCheck
{
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromDays(1);

    private readonly INuGetMetadataSource _source;
    private readonly IPackageCache _cache;
    private readonly IProgress<string>? _progress;
    private readonly Dictionary<string, NugetPackage?> _packages = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<PackageDto> _scannedPackages = [];

    public NugetCheck(INuGetMetadataSource? source = null, IPackageCache? cache = null, IProgress<string>? progress = null)
    {
        _source = source ?? new NuGetOrgMetadataSource();
        _cache = cache ?? new FilePackageCache();
        _progress = progress;
    }

    public async Task<NugetPackageResults> CheckPackageAndTransientsAsync(string mainPackageName, string mainPackageVersion)
    {
        var nugetPackageResults = new NugetPackageResults{ Warnings = [] };

        var mainPackage = await SearchPackageAsync(mainPackageName);
        if (mainPackage == null)
        {
            nugetPackageResults.Warnings.Add(new Warning
            {
                BreadCrumb = $"{mainPackageName} {mainPackageVersion}",
                Package = null,
                Message = "Package not found",
            });

            return nugetPackageResults;
        }

        nugetPackageResults.NugetPackageId = mainPackage.NugetPackageId;
        var mainPackageInfo = SearchPackageVersionInfo(mainPackage, mainPackageVersion);

        var mainBreadCrumb = $"{mainPackage.NugetPackageId} {mainPackageInfo?.OriginalVersion}".Trim();
        nugetPackageResults.Warnings.AddRange(GetRootPackageWarnings(mainPackageInfo, mainBreadCrumb));
        nugetPackageResults.NugetPackageOriginalVersion = mainPackageInfo?.OriginalVersion;

        if (mainPackageInfo?.DependencySets == null) return nugetPackageResults;

        var dependencyGroupsMessages = await GetDependencyGroupsMessagesAsync(mainPackageInfo.DependencySets, mainBreadCrumb);
        nugetPackageResults.Warnings.AddRange(dependencyGroupsMessages);
        return nugetPackageResults;
    }

    private async Task<List<Warning>> GetDependencyGroupsMessagesAsync(IEnumerable<DependencyGroupDto> dependencyGroups, string breadCrumb)
    {
        var warnings = new List<Warning>();
        foreach (var dependencyGroup in dependencyGroups)
        {
            foreach (var package in dependencyGroup.Packages)
            {
                var version = GetVersion(package.VersionRange);
                var alreadyChecked = _scannedPackages.Any(x => x.NugetPackageId == package.Id && x.Version == version);
                if (alreadyChecked) continue;

                _scannedPackages.Add(new PackageDto
                {
                    NugetPackageId = package.Id,
                    Version = version
                });

                var packageToScan = await SearchPackageAsync(package.Id);
                if (packageToScan == null)
                {
                    warnings.Add(new Warning
                    {
                        BreadCrumb = GetCurrentBreadCrumb(breadCrumb, package.Id, version),
                        Package = null,
                        Message = "Package not found",
                    });
                    continue;
                }

                var packageInfoToScan = SearchPackageVersionInfo(packageToScan, version);
                warnings.AddRange(GetRootPackageWarnings(packageInfoToScan, GetCurrentBreadCrumb(breadCrumb, package.Id, version)));

                if (packageInfoToScan?.DependencySets == null) continue;
                var packageInfoToScanWarnings = await GetDependencyGroupsMessagesAsync(packageInfoToScan.DependencySets, GetCurrentBreadCrumb(breadCrumb, package.Id, version));
                warnings.AddRange(packageInfoToScanWarnings);
            }
        }

        return warnings;
    }

    private static string GetCurrentBreadCrumb(string breadCrumb, string packageId, string packageVersion)
    {
        return $"{breadCrumb} > {packageId} {packageVersion}";
    }

    private string GetVersion(string dependencyDtoRangeValue)
    {
        var value = dependencyDtoRangeValue.Split(",")[0];
        value = value.Replace(">", "").Replace("[", "").Replace(")", "").Replace(",", "").Trim();
        return value;
    }

    private bool IsDeprecated(PackageMetadataRegistrationDto packageInfo)
    {
        return packageInfo.DeprecationMetadata != null;
    }

    public async Task<bool?> IsDeprecatedAsync(string packageName, string packageVersion)
    {
        var package = await SearchPackageAsync(packageName);
        if (package == null) return null;

        var packageInfo = SearchPackageVersionInfo(package, packageVersion);
        if (packageInfo == null) return null;
        return IsDeprecated(packageInfo);
    }

    private bool IsListed(PackageMetadataRegistrationDto? packageInfo)
    {
        if (packageInfo == null) return false;
        return packageInfo.IsListed;
    }

    public async Task<bool?> IsListedAsync(string packageName, string packageVersion)
    {
        var package = await SearchPackageAsync(packageName);
        if (package == null) return null;

        var packageInfo = SearchPackageVersionInfo(package, packageVersion);
        return IsListed(packageInfo);
    }

    private bool IsVulnerable(PackageMetadataRegistrationDto packageInfo)
    {
        return packageInfo.Vulnerabilities != null;
    }

    public async Task<bool?> IsVulnerableAsync(string packageName, string packageVersion)
    {
        var package = await SearchPackageAsync(packageName);
        if (package == null) return null;

        var packageInfo = SearchPackageVersionInfo(package, packageVersion);
        if (packageInfo == null) return null;
        return IsVulnerable(packageInfo);
    }

    private List<Warning> GetRootPackageWarnings(PackageMetadataRegistrationDto? packageInfo, string currentBreadCrumb)
    {
        var warnings = new List<Warning>();
        if (!IsListed(packageInfo))
        {
            warnings.Add(new Warning
            {
                Message = "Package is not listed",
                BreadCrumb = currentBreadCrumb,
                Package = packageInfo
            });
        }

        // if package info not found we cannot check for vulnerabilities or deprecation
        if (packageInfo == null) return warnings;

        if (IsVulnerable(packageInfo))
        {
            warnings.Add(new Warning
            {
                Message = "Package is vulnerable",
                BreadCrumb = currentBreadCrumb,
                Package = packageInfo
            });
        }

        if (IsDeprecated(packageInfo))
        {
            warnings.Add(new Warning
            {
                Message = "Package is deprecated",
                BreadCrumb = currentBreadCrumb,
                Package = packageInfo
            });
        }

        return warnings;
    }

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

    public PackageMetadataRegistrationDto? SearchPackageVersionInfo(NugetPackage package, string packageVersion)
    {
        _progress?.Report($"Scanning {package.NugetPackageId} {packageVersion}");
        return package.PackageMetadataRegistrations.FirstOrDefault(x => x.Identity.Version == packageVersion);
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

    public async Task<ProjectResults> CheckPackageAndTransientsAsync(string projectFilePath)
    {
        var packageReferences = GetProjectPackageList(projectFilePath);

        var packageReferencesResults = new List<NugetPackageResults>();
        foreach (var package in packageReferences)
        {
            var packageResults = await CheckPackageAndTransientsAsync(package.NugetPackageId, package.Version);
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
