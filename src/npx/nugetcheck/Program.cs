using System.Diagnostics;

// clean up .NET publish directory
var rootPath = Directory.GetParent(@"../../../../../");
var projectDir = $"{rootPath}\\BrainSharp.NugetCheck.Console";
var publishDir = $"{projectDir}\\bin\\Release\\net8.0\\publish";
if (Directory.Exists(publishDir)) Directory.Delete(publishDir, true);

// dotnet build -c Release
await ExecuteProcessAsync("dotnet", "build -c Release", projectDir);

// dotnet publish
await ExecuteProcessAsync("dotnet", "publish", projectDir);

// npm version patch
var npmFileName = "C:\\Program Files\\nodejs\\npm.cmd";
var npxProjectDir = Directory.GetParent(@"../../../");
var npxSkeletonDir = $"{npxProjectDir}\\npx-skeleton";

var version = await ExecuteProcessAsync(npmFileName, "version patch", npxSkeletonDir);

// fix version
version = version.Replace("\n", string.Empty);

// clone npm files to build directory
Console.WriteLine($"Cloning npm files to build directory...");
var npxBuildDir = $"{npxProjectDir}\\build\\{version}";
if (Directory.Exists(npxBuildDir)) Directory.Delete(npxBuildDir, true);
CopyDirectory(npxSkeletonDir, npxBuildDir);

// clone dotnet published files to build directory
Console.WriteLine($"Cloning dotnet published files to build directory...");
var consoleAppDir = $"{npxBuildDir}\\consoleapp";
CopyDirectory(publishDir, consoleAppDir);

// npm publish
Console.WriteLine($"run the above command to publish the npm package");
Console.WriteLine("npm publish " + npxBuildDir);

async Task<string> ExecuteProcessAsync(string fileName, string arguments, string workingDirectory)
{
    var process = new Process
    {
        StartInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            UseShellExecute = false
        }
    };

    process.Start();
    await process.WaitForExitAsync();
    var output = process.StandardOutput.ReadToEnd();
    Console.WriteLine(output);
    return output;
}

void CopyDirectory(string sourceDir, string destinationDir, bool recursive = true)
{
    var sourceDirInfo = new DirectoryInfo(sourceDir);
    if (!sourceDirInfo.Exists) throw new DirectoryNotFoundException($"Source directory does not exist or could not be found: {sourceDir}");

    Directory.CreateDirectory(destinationDir);

    // Get the files in the source directory and copy to the destination directory
    foreach (FileInfo file in sourceDirInfo.GetFiles())
    {
        string targetFilePath = Path.Combine(destinationDir, file.Name);
        file.CopyTo(targetFilePath);
    }

    // If recursive and copying subdirectories, recursively call this method on each subdirectory
    if (recursive)
    {
        DirectoryInfo[] subdirectories = sourceDirInfo.GetDirectories();
        foreach (DirectoryInfo subDir in subdirectories)
        {
            string newDestinationDir = Path.Combine(destinationDir, subDir.Name);
            CopyDirectory(subDir.FullName, newDestinationDir, true);
        }
    }
}



