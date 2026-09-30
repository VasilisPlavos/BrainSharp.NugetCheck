# BrainSharp.NugetCheck

[![CI](https://github.com/VasilisPlavos/BrainSharp.NugetCheck/actions/workflows/ci.yml/badge.svg)](https://github.com/VasilisPlavos/BrainSharp.NugetCheck/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/BrainSharp.NugetCheck)](https://www.nuget.org/packages/BrainSharp.NugetCheck)
[![npm](https://img.shields.io/npm/v/nugetscan)](https://www.npmjs.com/package/nugetscan)

Checks NuGet packages **and their transitive dependencies** for known vulnerabilities, deprecation and unlisted versions — from the command line or from C#.

## Command line

Requires Node.js and the [.NET 10 runtime](https://dotnet.microsoft.com/download) or newer.

```bash
npx nugetscan package SixLabors.ImageSharp --version 3.1.3   # a package and its transitive dependencies
npx nugetscan package Newtonsoft.Json --version 13.0.3 --framework net8.0   # only the dependencies net8.0 uses
npx nugetscan path/to/MyProject.csproj                        # every PackageReference of a project
npx nugetscan .                                               # every *.csproj below the current folder
npx nugetscan storage                                         # where the local cache is stored
```

Project scans read `<TargetFramework>` / `<TargetFrameworks>` and check only the dependencies NuGet restore would use for those frameworks. When the framework cannot be read (e.g. it comes from `Directory.Build.props`), or `package` is used without `--framework`, every dependency group is checked.

With [Central Package Management](https://learn.microsoft.com/nuget/consume-packages/central-package-management), versions come from the nearest `Directory.Packages.props` above the project: `Version` on the reference, then `VersionOverride`, then the central `PackageVersion`. `<GlobalPackageReference>` items are scanned in every project. `ManagePackageVersionsCentrally=false` in the project or the props file turns this off. Imports inside `Directory.Packages.props` are not followed.

`nugetcheck` is the same tool under a second name: `npx nugetcheck ...`.

| Exit code | Meaning |
| ----------- | --------- |
| 0 | No warnings |
| 1 | At least one warning (vulnerable, deprecated, unlisted, not found) |
| 2 | Invalid usage |
| 3 | The scan could not run (e.g. nuget.org unreachable, malformed project file) |

So `npx nugetscan .` can gate a CI pipeline.

## Library

```bash
dotnet add package BrainSharp.NugetCheck
```

```csharp
using BrainSharp.NugetCheck;

var nugetCheck = new NugetCheck();

var result = await nugetCheck.CheckPackageAndTransientsAsync("Newtonsoft.Json", "12.0.3");
var forNet8 = await nugetCheck.CheckPackageAndTransientsAsync("Newtonsoft.Json", "13.0.3", "net8.0");
foreach (var warning in result.Warnings)
    Console.WriteLine($"{warning.BreadCrumb}: {warning.Message}");

// null when the package or version does not exist
bool? vulnerable = await nugetCheck.IsVulnerableAsync("Newtonsoft.Json", "12.0.3");
```

Metadata is cached for 24 hours in the local application data folder. Pass your own `INuGetMetadataSource`, `IPackageCache` or `IProgress<string>` to the `NugetCheck` constructor to change where data comes from, where it is cached, or to receive progress messages.

## Development

```bash
cd src
dotnet build
dotnet test --filter "TestCategory!=Integration"   # fast, no network
dotnet test --filter "TestCategory=Integration"    # against nuget.org
```

## Roadmap

* Use more resources from <https://api.nuget.org/v3/index.json>
* <https://learn.microsoft.com/en-us/nuget/reference/nuget-client-sdk>
* <https://www.nuget.org/packages/NuGet.Protocol>
* <https://github.com/Azure/azure-cli/issues/24108>
* <https://www.google.com/search?q=nuget+credential+provider>
* <https://github.com/microsoft/artifacts-credprovider>

See [AGENTS.md](AGENTS.md) for architecture and conventions.

## License

MIT
