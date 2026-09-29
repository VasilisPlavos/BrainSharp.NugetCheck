using BrainSharp.NugetCheck.Entities;
using Newtonsoft.Json;

namespace BrainSharp.NugetCheck.Services;

/// <summary>Best-effort JSON cache: one file per package. Any read or write problem is treated as a cache miss.</summary>
public sealed class FilePackageCache : IPackageCache
{
    public static string DefaultLocation { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BrainSharp.NugetCheck", "cache");

    public FilePackageCache(string? location = null)
    {
        Location = location ?? DefaultLocation;
    }

    public string Location { get; }

    public async Task<NugetPackage?> GetAsync(string packageId, CancellationToken ct = default)
    {
        var filePath = GetFilePath(packageId);
        if (!File.Exists(filePath)) return null;

        try
        {
            var json = await File.ReadAllTextAsync(filePath, ct);
            return JsonConvert.DeserializeObject<NugetPackage>(json);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    public async Task SaveAsync(NugetPackage package, CancellationToken ct = default)
    {
        try
        {
            Directory.CreateDirectory(Location);
            await File.WriteAllTextAsync(GetFilePath(package.NugetPackageId), JsonConvert.SerializeObject(package), ct);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // the cache is an optimization; scanning works without it
        }
    }

    public void Clear()
    {
        if (Directory.Exists(Location)) Directory.Delete(Location, true);
    }

    private string GetFilePath(string packageId) => Path.Combine(Location, $"{packageId.ToLowerInvariant()}.json");
}
