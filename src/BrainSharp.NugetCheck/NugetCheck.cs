using System.Xml.Linq;
using BrainSharp.NugetCheck.Dtos;
using BrainSharp.NugetCheck.Entities;
using BrainSharp.NugetCheck.Services;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Packaging;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;

namespace BrainSharp.NugetCheck;

public class NugetCheck
{
    private readonly List<PackageDto> _scannedPackages = [];

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

        // TODO: Improve dat thing
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

    private async Task<List<Warning>> GetDependencyGroupsMessagesAsync(IEnumerable<PackageDependencyGroup> dependencyGroups, string breadCrumb)
    {
        var warnings = new List<Warning>();
        foreach (var dependencyGroup in dependencyGroups)
        {
            if (dependencyGroup.Packages == null) continue;

            foreach (var package in dependencyGroup.Packages)
            {
                var version = GetVersion(package.VersionRange.ToString());
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

    public async Task<NugetPackage?> SearchPackageAsync(string packageName)
    {
        var nugetPackage = await LocalStorageService.GetNugetPackageAsync(packageName);
        if (nugetPackage?.DateScanned > DateTime.UtcNow.AddDays(-1))
        {
            return nugetPackage;
        }
        
        // TODO: Jobs to be done! Improve dat thing
        var source = "https://api.nuget.org/v3/index.json";
        PackageSourceCredential? credentials = null;
        var packageSource = new PackageSource(source) { Credentials = credentials };
        var repository = Repository.Factory.GetCoreV3(packageSource);
        var resource = await repository.GetResourceAsync<PackageMetadataResource>();

        var packageSearchMetadata = await resource.GetMetadataAsync(packageName, true, true, new SourceCacheContext(), NullLogger.Instance, CancellationToken.None);
        if (packageSearchMetadata == null) return null;

        var packageMetadataRegistrations = packageSearchMetadata
            .Distinct()
            .Where(x => x.Identity.Id.ToLower() == packageName.ToLower())
            .Select(x => x as PackageSearchMetadataRegistration)
            .Select(x => new PackageMetadataRegistrationDto
            {
                DependencySets = x!.DependencySets,
                DeprecationMetadata = x.DeprecationMetadata,
                Identity = new NugetPackage2.Identity()
                {
                    Version = x.Identity.Version.ToString(),
                    Id = x.Identity.Id
                },
                OriginalVersion = x.Version.OriginalVersion!,
                Vulnerabilities = x.Vulnerabilities,
                IsListed = x.IsListed
            })
            .ToArray();

        if (!packageMetadataRegistrations.Any()) return null;

        nugetPackage = new NugetPackage
        {
            NugetPackageId = packageMetadataRegistrations.FirstOrDefault()!.Identity.Id,
            DateScanned = DateTime.UtcNow,
            PackageMetadataRegistrations = packageMetadataRegistrations
        };

        await LocalStorageService.SaveNugetPackageAsync(nugetPackage);
        return nugetPackage;
    }

    public PackageMetadataRegistrationDto? SearchPackageVersionInfo(NugetPackage package, string packageVersion)
    {
        try
        {
            Console.WriteLine(new string(' ', Console.WindowWidth));
            Console.SetCursorPosition(0, Console.CursorTop - 1);
            Console.WriteLine($"Scanning {package.NugetPackageId} {packageVersion}");
            Console.SetCursorPosition(0, Console.CursorTop - 1);
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
        }

        return package.PackageMetadataRegistrations.FirstOrDefault(x => x.Identity.Version.ToString() == packageVersion);
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