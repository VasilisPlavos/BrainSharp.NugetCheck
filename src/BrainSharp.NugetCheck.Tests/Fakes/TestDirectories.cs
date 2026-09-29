namespace BrainSharp.NugetCheck.Tests.Fakes;

public static class TestDirectories
{
    public static string Create()
    {
        var directory = Path.Combine(Path.GetTempPath(), "BrainSharp.NugetCheck.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    public static void Delete(string directory)
    {
        if (Directory.Exists(directory)) Directory.Delete(directory, true);
    }
}
