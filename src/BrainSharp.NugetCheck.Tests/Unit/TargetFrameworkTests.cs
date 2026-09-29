using BrainSharp.NugetCheck.Entities;
using BrainSharp.NugetCheck.Tests.Fakes;

namespace BrainSharp.NugetCheck.Tests.Unit;

public class TargetFrameworkTests
{
    private static NugetCheck CreateNugetCheck(FakeMetadataSource source) => new(source, new InMemoryPackageCache());

    private static string[] BreadCrumbs(NugetPackageResults results) => results.Warnings.Select(x => x.BreadCrumb).ToArray();

    // A 1.0.0 depends on Old (vulnerable) for netstandard1.0 and on New (clean) for netstandard2.0.
    private static FakeMetadataSource PackageWithLegacyGroup() => new FakeMetadataSource()
        .WithPackage("A", "1.0.0")
        .WithDependencyGroup("A", "1.0.0", "netstandard1.0", "Old [1.0.0, )")
        .WithDependencyGroup("A", "1.0.0", "netstandard2.0", "New [1.0.0, )")
        .WithPackage("Old", "1.0.0", vulnerable: true)
        .WithPackage("New", "1.0.0");

    [Test]
    public async Task CheckPackageAndTransientsAsync_WithoutFramework_WalksEveryGroup()
    {
        var results = await CreateNugetCheck(PackageWithLegacyGroup()).CheckPackageAndTransientsAsync("A", "1.0.0");

        Assert.That(BreadCrumbs(results), Is.EqualTo(new[] { "A 1.0.0 > Old 1.0.0" }));
    }

    [TestCase("net8.0")]
    [TestCase("net8.0-windows")]
    public async Task CheckPackageAndTransientsAsync_WithFramework_WalksOnlyNearestGroup(string framework)
    {
        var source = PackageWithLegacyGroup();

        var results = await CreateNugetCheck(source).CheckPackageAndTransientsAsync("A", "1.0.0", framework);

        Assert.Multiple(() =>
        {
            Assert.That(results.Warnings, Is.Empty);
            Assert.That(source.CallsFor("Old"), Is.Zero);
            Assert.That(source.CallsFor("New"), Is.EqualTo(1));
        });
    }

    [Test]
    public async Task CheckPackageAndTransientsAsync_TransitiveDependency_UsesNearestGroupAtEveryLevel()
    {
        var source = PackageWithLegacyGroup().WithPackage("Root", "1.0.0", ["A [1.0.0, )"]);

        var results = await CreateNugetCheck(source).CheckPackageAndTransientsAsync("Root", "1.0.0", "net8.0");

        Assert.That(results.Warnings, Is.Empty);
        Assert.That(source.CallsFor("Old"), Is.Zero);
    }

    [Test]
    public async Task CheckPackageAndTransientsAsync_NoCompatibleGroup_WalksNoDependencies()
    {
        var source = new FakeMetadataSource()
            .WithPackage("A", "1.0.0")
            .WithDependencyGroup("A", "1.0.0", "net48", "Old [1.0.0, )")
            .WithPackage("Old", "1.0.0", vulnerable: true);

        var results = await CreateNugetCheck(source).CheckPackageAndTransientsAsync("A", "1.0.0", "net8.0");

        Assert.That(results.Warnings, Is.Empty);
        Assert.That(source.CallsFor("Old"), Is.Zero);
    }

    [Test]
    public async Task CheckPackageAndTransientsAsync_AnyGroup_IsUsed()
    {
        var source = new FakeMetadataSource()
            .WithPackage("A", "1.0.0")
            .WithDependencyGroup("A", "1.0.0", "any", "Old [1.0.0, )")
            .WithPackage("Old", "1.0.0", vulnerable: true);

        var results = await CreateNugetCheck(source).CheckPackageAndTransientsAsync("A", "1.0.0", "net8.0");

        Assert.That(BreadCrumbs(results), Is.EqualTo(new[] { "A 1.0.0 > Old 1.0.0" }));
    }

    [Test]
    public void CheckPackageAndTransientsAsync_UnsupportedFramework_ThrowsArgumentException()
    {
        var nugetCheck = CreateNugetCheck(new FakeMetadataSource().WithPackage("A", "1.0.0"));

        Assert.ThrowsAsync<ArgumentException>(() => nugetCheck.CheckPackageAndTransientsAsync("A", "1.0.0", "notaframework"));
    }

    [TestCase("net8.0", true)]
    [TestCase("net8", true)]
    [TestCase("net8.0-windows", true)]
    [TestCase("net48", true)]
    [TestCase("netstandard2.0", true)]
    [TestCase("notaframework", false)]
    [TestCase("any", false)]
    [TestCase("", false)]
    [TestCase("$(TargetFramework)", false)]
    public void IsSupportedTargetFramework_Value_ReturnsExpected(string value, bool expected)
    {
        Assert.That(NugetCheck.IsSupportedTargetFramework(value), Is.EqualTo(expected));
    }
}
