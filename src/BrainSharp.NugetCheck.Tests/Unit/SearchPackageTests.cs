using BrainSharp.NugetCheck.Tests.Fakes;

namespace BrainSharp.NugetCheck.Tests.Unit;

public class SearchPackageTests
{
    [Test]
    public async Task SearchPackageAsync_NoCacheEntry_FetchesAndCaches()
    {
        var source = new FakeMetadataSource().WithPackage("A", "1.0.0");
        var cache = new InMemoryPackageCache();

        var package = await new NugetCheck(source, cache).SearchPackageAsync("A");
        var cached = await cache.GetAsync("A");

        Assert.Multiple(() =>
        {
            Assert.That(package?.NugetPackageId, Is.EqualTo("A"));
            Assert.That(source.CallsFor("A"), Is.EqualTo(1));
            Assert.That(cached, Is.Not.Null);
        });
    }

    [Test]
    public async Task SearchPackageAsync_FreshCacheEntry_DoesNotCallSource()
    {
        var source = new FakeMetadataSource().WithPackage("A", "1.0.0");
        var cache = new InMemoryPackageCache();
        await cache.SaveAsync(TestPackages.Create(source, "A", DateTime.UtcNow.AddHours(-1)));

        var package = await new NugetCheck(source, cache).SearchPackageAsync("A");

        Assert.That(package, Is.Not.Null);
        Assert.That(source.CallsFor("A"), Is.EqualTo(0));
    }

    [Test]
    public async Task SearchPackageAsync_StaleCacheEntry_FetchesAgain()
    {
        var source = new FakeMetadataSource().WithPackage("A", "1.0.0");
        var cache = new InMemoryPackageCache();
        await cache.SaveAsync(TestPackages.Create(source, "A", DateTime.UtcNow.AddDays(-2)));

        await new NugetCheck(source, cache).SearchPackageAsync("A");

        Assert.That(source.CallsFor("A"), Is.EqualTo(1));
        Assert.That((await cache.GetAsync("A"))!.DateScanned, Is.GreaterThan(DateTime.UtcNow.AddMinutes(-1)));
    }

    [Test]
    public async Task SearchPackageAsync_UnknownPackage_ReturnsNull()
    {
        var package = await new NugetCheck(new FakeMetadataSource(), new InMemoryPackageCache()).SearchPackageAsync("Missing");

        Assert.That(package, Is.Null);
    }

    [Test]
    public async Task SearchPackageAsync_UnknownPackageTwice_CallsSourceOnce()
    {
        var source = new FakeMetadataSource();
        var nugetCheck = new NugetCheck(source, new InMemoryPackageCache());

        await nugetCheck.SearchPackageAsync("Missing");
        await nugetCheck.SearchPackageAsync("Missing");

        Assert.That(source.CallsFor("Missing"), Is.EqualTo(1));
    }

    [Test]
    public async Task SearchPackageAsync_DifferentCase_ReturnsCanonicalIdAndFetchesOnce()
    {
        var source = new FakeMetadataSource().WithPackage("Newtonsoft.Json", "13.0.3");
        var cache = new InMemoryPackageCache();

        var first = await new NugetCheck(source, cache).SearchPackageAsync("newtonsoft.json");
        var second = await new NugetCheck(source, cache).SearchPackageAsync("NEWTONSOFT.JSON");

        Assert.Multiple(() =>
        {
            Assert.That(first?.NugetPackageId, Is.EqualTo("Newtonsoft.Json"));
            Assert.That(second?.NugetPackageId, Is.EqualTo("Newtonsoft.Json"));
            Assert.That(source.CallsFor("Newtonsoft.Json"), Is.EqualTo(1));
        });
    }
}
