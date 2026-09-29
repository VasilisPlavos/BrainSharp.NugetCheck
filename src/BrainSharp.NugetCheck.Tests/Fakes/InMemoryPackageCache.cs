using BrainSharp.NugetCheck.Entities;
using BrainSharp.NugetCheck.Services;

namespace BrainSharp.NugetCheck.Tests.Fakes;

public class InMemoryPackageCache : IPackageCache
{
    private readonly Dictionary<string, NugetPackage> _packages = new(StringComparer.OrdinalIgnoreCase);

    public string Location => "memory";

    public Task<NugetPackage?> GetAsync(string packageId, CancellationToken ct = default) =>
        Task.FromResult(_packages.GetValueOrDefault(packageId));

    public Task SaveAsync(NugetPackage package, CancellationToken ct = default)
    {
        _packages[package.NugetPackageId] = package;
        return Task.CompletedTask;
    }

    public void Clear() => _packages.Clear();
}
