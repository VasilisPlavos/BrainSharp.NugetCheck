using System.Diagnostics;

// Builds the npm packages that wrap BrainSharp.NugetCheck.ConsoleApp.
// Both npm names ship the same CLI. Run: dotnet run --project src/npx/NpxPublisher
// It bumps the patch version in src/npx/<name>/package.json (commit that) and prints the `npm publish` commands.

string[] npmPackageNames = ["nugetscan", "nugetcheck"];

var srcDir = FindSourceDirectory();
var npxDir = Path.Combine(srcDir, "npx");
var buildRootDir = Path.Combine(npxDir, "build");
var publishDir = Path.Combine(buildRootDir, "publish");
var consoleProject = Path.Combine(srcDir, "BrainSharp.NugetCheck.Console", "BrainSharp.NugetCheck.ConsoleApp.csproj");

if (Directory.Exists(publishDir)) Directory.Delete(publishDir, true);
await RunAsync("dotnet", $"publish \"{consoleProject}\" -c Release -o \"{publishDir}\"", srcDir);

// npm is a batch file on Windows: started directly, npm.cmd looks for npm-cli.js in the working directory, so let cmd.exe run it
var (npm, npmArgsPrefix) = OperatingSystem.IsWindows() ? ("cmd.exe", "/c npm ") : ("npm", "");
var publishCommands = new List<string>();

foreach (var name in npmPackageNames)
{
    var packageDir = Path.Combine(npxDir, name);
    var version = LastLine(await RunAsync(npm, npmArgsPrefix + "version patch --no-git-tag-version", packageDir));

    var buildDir = Path.Combine(buildRootDir, name, version);
    Console.WriteLine($"Creating {buildDir}...");
    if (Directory.Exists(buildDir)) Directory.Delete(buildDir, true);

    var consoleAppDir = Path.Combine(buildDir, "consoleapp");
    CopyDirectory(publishDir, consoleAppDir);
    File.Copy(Path.Combine(npxDir, "shared", "app.js"), Path.Combine(consoleAppDir, "app.js"));
    File.Copy(Path.Combine(npxDir, "shared", "LICENSE"), Path.Combine(buildDir, "LICENSE"));
    File.Copy(Path.Combine(packageDir, "package.json"), Path.Combine(buildDir, "package.json"));
    File.Copy(Path.Combine(packageDir, "README.md"), Path.Combine(buildDir, "README.md"));

    publishCommands.Add($"npm publish \"{buildDir}\"");
}

Console.WriteLine();
Console.WriteLine("Run these commands to publish the npm packages:");
publishCommands.ForEach(Console.WriteLine);
return;

static string FindSourceDirectory()
{
    for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
    {
        if (File.Exists(Path.Combine(dir.FullName, "BrainSharp.NugetCheck.sln"))) return dir.FullName;
    }

    throw new DirectoryNotFoundException($"BrainSharp.NugetCheck.sln not found above {AppContext.BaseDirectory}");
}

static string LastLine(string output) =>
    output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Last();

static async Task<string> RunAsync(string fileName, string arguments, string workingDirectory)
{
    var startInfo = new ProcessStartInfo(fileName, arguments)
    {
        WorkingDirectory = workingDirectory,
        RedirectStandardOutput = true,
        UseShellExecute = false
    };

    using var process = Process.Start(startInfo) ?? throw new InvalidOperationException($"Could not start {fileName}");

    // read before waiting: a full stdout pipe would otherwise block the child forever
    var output = await process.StandardOutput.ReadToEndAsync();
    await process.WaitForExitAsync();
    Console.Write(output);

    if (process.ExitCode != 0) throw new InvalidOperationException($"'{fileName} {arguments}' exited with code {process.ExitCode}");
    return output;
}

static void CopyDirectory(string sourceDir, string destinationDir)
{
    Directory.CreateDirectory(destinationDir);
    foreach (var file in Directory.GetFiles(sourceDir)) File.Copy(file, Path.Combine(destinationDir, Path.GetFileName(file)));
    foreach (var dir in Directory.GetDirectories(sourceDir)) CopyDirectory(dir, Path.Combine(destinationDir, Path.GetFileName(dir)));
}
