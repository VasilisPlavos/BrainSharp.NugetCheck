# nugetscan

Command line tool that checks NuGet packages **and their transitive dependencies** for known vulnerabilities, deprecation and unlisted versions.

Source code and issues: [github.com/VasilisPlavos/BrainSharp.NugetCheck](https://github.com/VasilisPlavos/BrainSharp.NugetCheck)

## Requirements

- Node.js (for `npx`)
- [.NET 10 runtime](https://dotnet.microsoft.com/download) or newer

## Usage

```
npx nugetscan package SixLabors.ImageSharp --version 3.1.3   # a package and its transitive dependencies
npx nugetscan path/to/MyProject.csproj                        # every PackageReference of a project
npx nugetscan .                                               # every *.csproj below the current folder (bin, obj and node_modules are skipped)
npx nugetscan storage                                         # where the local cache is stored
```

Try `SixLabors.ImageSharp` `3.1.3` (vulnerable) against `3.1.4` to see the difference.

## Exit codes

| Code | Meaning |
|------|---------|
| 0 | No warnings |
| 1 | At least one warning: vulnerable, deprecated, unlisted, or not found |
| 2 | Invalid usage |

Any other non-zero code means the scan could not run (for example nuget.org was unreachable, or `dotnet` is not installed).

Use it as a CI gate: `npx nugetscan .`

## Cache

Package metadata from nuget.org is cached for 24 hours in the folder printed by `npx nugetscan storage`, so repeated scans are fast.
