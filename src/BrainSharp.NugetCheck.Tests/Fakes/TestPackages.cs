using BrainSharp.NugetCheck.Entities;

namespace BrainSharp.NugetCheck.Tests.Fakes;

public static class TestPackages
{
    public static NugetPackage Create(FakeMetadataSource source, string id, DateTime dateScanned) => new()
    {
        NugetPackageId = id,
        DateScanned = dateScanned,
        PackageMetadataRegistrations = source.VersionsOf(id)
    };
}
