# AGENTS.md

Guidance for AI coding agents working in this repository.

## What this is

BrainSharp.NugetCheck checks NuGet packages and their transitive dependencies for known vulnerabilities, deprecation and unlisted versions, using the nuget.org V3 API through `NuGet.Protocol`.

It ships in three forms:

- **NuGet library** `BrainSharp.NugetCheck` — `src/BrainSharp.NugetCheck`
- **Console app** `BrainSharp.NugetCheck.ConsoleApp` — `src/BrainSharp.NugetCheck.Console`
- **npm wrappers** `nugetscan` and `nugetcheck` — `src/npx/*`; they run the console app with `dotnet`

## Commands (run from `src/`)

```bash
dotnet build
dotnet test    # currently every test calls nuget.org; some take 30+ seconds
dotnet run --project BrainSharp.NugetCheck.Console -- package Newtonsoft.Json --version 12.0.3
```

## Layout

- `BrainSharp.NugetCheck/NugetCheck.cs` — the whole engine: reads `PackageReference`s from a csproj, fetches metadata, walks dependencies, produces warnings.
- `BrainSharp.NugetCheck/Services/LocalStorageService.cs` — JSON file cache next to the executable.
- `BrainSharp.NugetCheck.Console/Program.cs` — argument handling; `Processors.cs` — console report.
- `npx/<name>/Program.cs` — local release script that publishes the console app into an npm package folder.

## Conventions

- Test names: `Method_Condition_Expectation` (NUnit).
- Nullable reference types and implicit usings are enabled in all projects.
- Do not rename the console app assembly `BrainSharp.NugetCheck.ConsoleApp`: the npm `app.js` starts it by file name.
- Design specs and plans live in `docs/superpowers/` and are git-ignored — never commit them.
