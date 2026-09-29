namespace BrainSharp.NugetCheck.ConsoleApp;

public static class ProjectFinder
{
    // build output and npm folders contain copied or generated project files
    private static readonly HashSet<string> IgnoredDirectories = new(StringComparer.OrdinalIgnoreCase) { "bin", "obj", "node_modules" };

    public static List<string> FindProjectFiles(string rootDirectory) =>
        Directory.EnumerateFiles(rootDirectory, "*.csproj", new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true })
            .Where(file => !IsInIgnoredDirectory(rootDirectory, file))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static bool IsInIgnoredDirectory(string rootDirectory, string file) =>
        Path.GetRelativePath(rootDirectory, Path.GetDirectoryName(file)!)
            .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(IgnoredDirectories.Contains);
}
