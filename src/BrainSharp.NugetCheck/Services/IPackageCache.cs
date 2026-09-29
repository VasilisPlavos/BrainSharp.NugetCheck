using BrainSharp.NugetCheck.Entities;

namespace BrainSharp.NugetCheck.Services;

public interface IPackageCache
{
    string Location { get; }
    Task<NugetPackage?> GetAsync(string packageId, CancellationToken ct = default);
    Task SaveAsync(NugetPackage package, CancellationToken ct = default);
    void Clear();
}
