# AGENTS.md

Guidance for AI coding agents (and humans) working in this repository.

## What this is

BrainSharp.NugetCheck checks NuGet packages and their transitive dependencies for known vulnerabilities, deprecation and unlisted versions, using the nuget.org V3 API through `NuGet.Protocol`.

It ships in three forms:

- **NuGet library** `BrainSharp.NugetCheck` — `src/BrainSharp.NugetCheck` (net8.0 + net10.0)
- **Console app** `BrainSharp.NugetCheck.ConsoleApp` — `src/BrainSharp.NugetCheck.Console` (net10.0, RollForward=Major)
- **npm packages** `nugetscan` and `nugetcheck` — the same CLI under two names; `src/npx/shared/app.js` runs `dotnet BrainSharp.NugetCheck.ConsoleApp.dll`

## Commands (run from `src/`)

| Task | Command |
| ------ | --------- |
| Build (library and CLI treat warnings as errors) | `dotnet build` |
| Unit tests (no network, fast) | `dotnet test --filter "TestCategory!=Integration"` |
| Integration tests (real nuget.org) | `dotnet test --filter "TestCategory=Integration"` |
| Run the CLI | `dotnet run --project BrainSharp.NugetCheck.Console -- package Newtonsoft.Json --version 12.0.3` |
| Pack the library | `dotnet pack BrainSharp.NugetCheck -c Release` |
| Build the npm packages | `dotnet run --project npx/NpxPublisher` (bumps patch versions, prints `npm publish` commands) |

CI (`.github/workflows/ci.yml`) runs build, unit tests and pack on Ubuntu and Windows; integration tests run weekly and on manual dispatch.

## Architecture

- `NugetCheck` — public entry point. For each root package it resolves the version, collects warnings and walks every dependency group recursively. Transitive dedup restarts per root package, so project results do not depend on reference order. Not thread-safe.
- `INuGetMetadataSource` → `NuGetOrgMetadataSource` — the only code that talks to NuGet.Protocol; it maps NuGet types to our DTOs.
- `IPackageCache` → `FilePackageCache` — one JSON file per lower-cased package id under `LocalApplicationData/BrainSharp.NugetCheck/cache`; entries are fresh for 1 day; any I/O or JSON error is a cache miss.
- Version resolution — dependency ranges use `VersionRange.FindBestMatch` (NuGet's lowest applicable version). Root versions: an exact version must exist (compared as `NuGetVersion`, so `4.0` == `4.0.0`); pins, ranges and floats (`[1.0.0]`, `1.*`) resolve like NuGet.
- Target frameworks — project scans read `TargetFramework(s)` (union over property groups, `$(...)` skipped) and walk each package's nearest dependency group per framework via `NuGetFrameworkUtility.GetNearest`, one walk per framework, warnings de-duplicated by message + breadcrumb. No framework → every group (also `package` without `--framework`).
- Central Package Management — `CentralPackageVersions` reads the nearest `Directory.Packages.props` above the project (no merge with parent props, `<Import>` not followed). Version order: `Version` on the reference, `VersionOverride`, central `PackageVersion` (last one wins, conditions ignored). `GlobalPackageReference` items are appended unless the project references the same id. CPM is on when a props file exists, unless `ManagePackageVersionsCentrally=false` (the project's value wins over the props file's). `ProjectXml` reads MSBuild items for both.
- Warnings — exact strings in `Entities/WarningMessages.cs`.
- CLI — `CommandLineParser` → `CliCommand` records → `Program` → `Processors` (console report). Exit codes in `ExitCodes`: 0 no warnings, 1 warnings, 2 invalid usage, 3 scan could not run (a directory scan keeps going past a failing project). `Program.RunAsync` is the testable entry point. `ProjectFinder` skips `bin`, `obj`, `node_modules`.

## Conventions

- Test names `Method_Condition_Expectation`; NUnit 4 constraint model (`Assert.That`).
- Unit tests use `Fakes/FakeMetadataSource` and `Fakes/InMemoryPackageCache` — never the network. Tests that need nuget.org get `[Category("Integration")]` and assert stable facts only, never warning counts.
- No `Console` in the library; report progress through `IProgress<string>`.
- Cached DTOs must round-trip through Newtonsoft.Json. Do not put NuGet types that contain `VersionRange` or `NuGetFramework` in them (e.g. `PackageDependencyGroup`, `PackageDeprecationMetadata`) — they cannot be deserialized; map them to DTOs in `NuGetOrgMetadataSource`.
- Do not rename the console app assembly; `app.js` starts it by file name.
- `docs/superpowers/` holds local design specs and plans and is git-ignored — never commit it.
