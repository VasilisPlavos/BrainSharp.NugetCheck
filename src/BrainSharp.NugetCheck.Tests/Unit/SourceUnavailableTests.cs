using BrainSharp.NugetCheck.Entities;
using BrainSharp.NugetCheck.Services;
using BrainSharp.NugetCheck.Tests.Fakes;

namespace BrainSharp.NugetCheck.Tests.Unit;

public class SourceUnavailableTests
{
    private static (string Message, string BreadCrumb)[] Warnings(NugetPackageResults results) =>
        results.Warnings.Select(x => (x.Message, x.BreadCrumb)).ToArray();

    [Test]
    public async Task CheckPackageAndTransientsAsync_UnreachableRoot_ReturnsNotChecked()
    {
        var source = new FakeMetadataSource().WithUnreachable("A");

        var results = await new NugetCheck(source, new InMemoryPackageCache()).CheckPackageAndTransientsAsync("A", "1.0.0");

        Assert.That(Warnings(results), Is.EqualTo(new[] { (WarningMessages.NotChecked, "A 1.0.0") }));
    }

    [Test]
    public async Task CheckPackageAndTransientsAsync_UnreachableDependency_ReturnsNotCheckedAndChecksFreshCachedPackages()
    {
        var source = new FakeMetadataSource()
            .WithPackage("A", "1.0.0", ["B [1.0.0, )", "C [1.0.0, )"])
            .WithPackage("C", "1.0.0", vulnerable: true)
            .WithUnreachable("B");
        var cache = new InMemoryPackageCache();
        await cache.SaveAsync(TestPackages.Create(source, "C", DateTime.UtcNow.AddHours(-1)));

        var results = await new NugetCheck(source, cache).CheckPackageAndTransientsAsync("A", "1.0.0");

        Assert.That(Warnings(results), Is.EqualTo(new[]
        {
            (WarningMessages.NotChecked, "A 1.0.0 > B [1.0.0, )"),
            (WarningMessages.Vulnerable, "A 1.0.0 > C 1.0.0")
        }));
    }

    [Test]
    public async Task CheckPackageAndTransientsAsync_AfterFirstFailure_DoesNotCallSourceAgain()
    {
        var source = new FakeMetadataSource()
            .WithPackage("A", "1.0.0", ["B [1.0.0, )", "C [1.0.0, )"])
            .WithPackage("C", "1.0.0")
            .WithUnreachable("B");
        var cache = new InMemoryPackageCache();
        await cache.SaveAsync(TestPackages.Create(source, "A", DateTime.UtcNow.AddHours(-1)));

        var results = await new NugetCheck(source, cache).CheckPackageAndTransientsAsync("A", "1.0.0");

        Assert.Multiple(() =>
        {
            Assert.That(source.CallsFor("B"), Is.EqualTo(1));
            Assert.That(source.CallsFor("C"), Is.EqualTo(0));
            Assert.That(Warnings(results), Is.EqualTo(new[]
            {
                (WarningMessages.NotChecked, "A 1.0.0 > B [1.0.0, )"),
                (WarningMessages.NotChecked, "A 1.0.0 > C [1.0.0, )")
            }));
        });
    }

    [Test]
    public async Task CheckPackageAndTransientsAsync_StaleCacheEntryAndUnreachableSource_ReturnsNotChecked()
    {
        var source = new FakeMetadataSource().WithPackage("A", "1.0.0", vulnerable: true);
        var cache = new InMemoryPackageCache();
        await cache.SaveAsync(TestPackages.Create(source, "A", DateTime.UtcNow.AddDays(-2)));
        source.WithUnreachable("A");

        var results = await new NugetCheck(source, cache).CheckPackageAndTransientsAsync("A", "1.0.0");

        Assert.That(Warnings(results), Is.EqualTo(new[] { (WarningMessages.NotChecked, "A 1.0.0") }));
    }

    [Test]
    public void SearchPackageAsync_UnreachableSource_ThrowsEveryTimeInsteadOfReturningNotFound()
    {
        var nugetCheck = new NugetCheck(new FakeMetadataSource().WithUnreachable("A"), new InMemoryPackageCache());

        Assert.Multiple(() =>
        {
            Assert.ThrowsAsync<PackageSourceUnavailableException>(() => nugetCheck.SearchPackageAsync("A"));
            Assert.ThrowsAsync<PackageSourceUnavailableException>(() => nugetCheck.SearchPackageAsync("A"));
        });
    }

    [Test]
    public async Task CheckPackageAndTransientsAsync_NonNetworkFailure_ThrowsAndKeepsCallingSource()
    {
        var source = new FakeMetadataSource()
            .WithPackage("A", "1.0.0")
            .WithFailure("Broken", new InvalidDataException("malformed registration page"));
        var nugetCheck = new NugetCheck(source, new InMemoryPackageCache());

        Assert.That(() => nugetCheck.CheckPackageAndTransientsAsync("Broken", "1.0.0"), Throws.InstanceOf<InvalidDataException>());
        var results = await nugetCheck.CheckPackageAndTransientsAsync("A", "1.0.0");

        Assert.That(results.Warnings, Is.Empty);
    }

    [Test]
    public void CheckPackageAndTransientsAsync_Cancelled_ThrowsInsteadOfNotChecked()
    {
        var source = new FakeMetadataSource().WithPackage("A", "1.0.0");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var nugetCheck = new NugetCheck(source, new InMemoryPackageCache());

        Assert.That(() => nugetCheck.CheckPackageAndTransientsAsync("A", "1.0.0", cancellation.Token),
            Throws.InstanceOf<OperationCanceledException>());
    }
}
