using BrainSharp.NugetCheck.Services;
using BrainSharp.NugetCheck.Tests.Fakes;

namespace BrainSharp.NugetCheck.Tests.Unit;

public class FilePackageCacheTests
{
    private string _directory = null!;

    [SetUp]
    public void SetUp() => _directory = TestDirectories.Create();

    [TearDown]
    public void TearDown() => TestDirectories.Delete(_directory);

    private static FakeMetadataSource SourceWithFlaggedPackage() => new FakeMetadataSource()
        .WithPackage("A", "1.0.0", dependencies: ["B [2.0.0, )"], vulnerable: true, deprecated: true, listed: false);

    [Test]
    public async Task SaveAsync_PackageWithDependenciesAndAdvisories_RoundTrips()
    {
        var package = TestPackages.Create(SourceWithFlaggedPackage(), "A", DateTime.UtcNow);
        var cache = new FilePackageCache(_directory);

        await cache.SaveAsync(package);
        var loaded = await cache.GetAsync("A");

        Assert.That(loaded, Is.Not.Null);
        var version = loaded!.PackageMetadataRegistrations.Single();
        var dependency = version.DependencySets.Single().Packages.Single();
        Assert.Multiple(() =>
        {
            Assert.That(loaded.NugetPackageId, Is.EqualTo("A"));
            Assert.That(loaded.DateScanned, Is.EqualTo(package.DateScanned));
            Assert.That(version.Identity.Version, Is.EqualTo("1.0.0"));
            Assert.That(version.IsListed, Is.False);
            Assert.That(dependency.Id, Is.EqualTo("B"));
            Assert.That(dependency.VersionRange, Is.EqualTo("[2.0.0, )"));
            Assert.That(version.Vulnerabilities!.Single().Severity, Is.EqualTo(2));
            Assert.That(version.DeprecationMetadata!.Reasons, Is.EquivalentTo(new[] { "Legacy" }));
            Assert.That(version.DeprecationMetadata.AlternatePackageId, Is.EqualTo("B"));
            Assert.That(version.DeprecationMetadata.AlternatePackageVersionRange, Is.EqualTo("[2.0.0, )"));
        });
    }

    [Test]
    public async Task GetAsync_DifferentCase_ReturnsPackage()
    {
        var cache = new FilePackageCache(_directory);
        await cache.SaveAsync(TestPackages.Create(SourceWithFlaggedPackage(), "A", DateTime.UtcNow));

        Assert.That(await cache.GetAsync("a"), Is.Not.Null);
    }

    [Test]
    public async Task GetAsync_UnknownPackage_ReturnsNull()
    {
        var cache = new FilePackageCache(_directory);

        Assert.That(await cache.GetAsync("Unknown"), Is.Null);
    }

    [Test]
    public async Task GetAsync_CorruptFile_ReturnsNull()
    {
        var cache = new FilePackageCache(_directory);
        await cache.SaveAsync(TestPackages.Create(SourceWithFlaggedPackage(), "A", DateTime.UtcNow));
        await File.WriteAllTextAsync(Directory.GetFiles(_directory).Single(), "{ \"NugetPackageId\": \"A\", \"Package");

        Assert.That(await cache.GetAsync("A"), Is.Null);
    }

    [Test]
    public void SaveAsync_LocationNotWritable_DoesNotThrow()
    {
        var blockingFile = Path.Combine(_directory, "not-a-directory");
        File.WriteAllText(blockingFile, "");
        var cache = new FilePackageCache(Path.Combine(blockingFile, "cache"));
        var package = TestPackages.Create(SourceWithFlaggedPackage(), "A", DateTime.UtcNow);

        Assert.DoesNotThrowAsync(() => cache.SaveAsync(package));
        Assert.DoesNotThrowAsync(() => cache.GetAsync("A"));
    }

    [Test]
    public void ResolveDefaultLocation_NoLocalApplicationData_UsesAbsoluteTempPath()
    {
        // on Linux containers without a home folder GetFolderPath returns ""; never fall back to the current directory
        var location = FilePackageCache.ResolveDefaultLocation("");

        Assert.That(Path.IsPathRooted(location), Is.True);
        Assert.That(location, Does.StartWith(Path.GetTempPath()));
    }

    [Test]
    public void ResolveDefaultLocation_LocalApplicationData_UsesIt()
    {
        var localApplicationData = Path.Combine(_directory, "LocalAppData");

        Assert.That(FilePackageCache.ResolveDefaultLocation(localApplicationData),
            Is.EqualTo(Path.Combine(localApplicationData, "BrainSharp.NugetCheck", "cache")));
    }

    [Test]
    public async Task Clear_AfterSave_RemovesPackages()
    {
        var cache = new FilePackageCache(_directory);
        await cache.SaveAsync(TestPackages.Create(SourceWithFlaggedPackage(), "A", DateTime.UtcNow));

        cache.Clear();

        Assert.That(await cache.GetAsync("A"), Is.Null);
    }
}
