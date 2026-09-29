using BrainSharp.NugetCheck.Entities;
using BrainSharp.NugetCheck.Tests.Fakes;

namespace BrainSharp.NugetCheck.Tests.Unit;

public class CheckPackageTests
{
    private static NugetCheck CreateNugetCheck(FakeMetadataSource source, IProgress<string>? progress = null) =>
        new(source, new InMemoryPackageCache(), progress);

    private static string[] Messages(NugetPackageResults results) => results.Warnings.Select(x => x.Message).ToArray();

    private static string[] BreadCrumbs(NugetPackageResults results) => results.Warnings.Select(x => x.BreadCrumb).ToArray();

    [Test]
    public async Task CheckPackageAndTransientsAsync_CleanPackage_ReturnsNoWarnings()
    {
        var source = new FakeMetadataSource().WithPackage("A", "1.0.0");

        var results = await CreateNugetCheck(source).CheckPackageAndTransientsAsync("A", "1.0.0");

        Assert.Multiple(() =>
        {
            Assert.That(results.NugetPackageId, Is.EqualTo("A"));
            Assert.That(results.NugetPackageOriginalVersion, Is.EqualTo("1.0.0"));
            Assert.That(results.Warnings, Is.Empty);
        });
    }

    [Test]
    public async Task CheckPackageAndTransientsAsync_UnknownPackage_ReturnsPackageNotFound()
    {
        var results = await CreateNugetCheck(new FakeMetadataSource()).CheckPackageAndTransientsAsync("Missing", "1.0.0");

        Assert.Multiple(() =>
        {
            Assert.That(results.NugetPackageId, Is.EqualTo("Missing"));
            Assert.That(Messages(results), Is.EqualTo(new[] { WarningMessages.PackageNotFound }));
            Assert.That(BreadCrumbs(results), Is.EqualTo(new[] { "Missing 1.0.0" }));
        });
    }

    [Test]
    public async Task CheckPackageAndTransientsAsync_UnknownVersion_ReturnsVersionNotFound()
    {
        var source = new FakeMetadataSource().WithPackage("A", "1.0.0");

        var results = await CreateNugetCheck(source).CheckPackageAndTransientsAsync("A", "2.0.0");

        Assert.That(Messages(results), Is.EqualTo(new[] { WarningMessages.VersionNotFound }));
        Assert.That(BreadCrumbs(results), Is.EqualTo(new[] { "A 2.0.0" }));
    }

    [Test]
    public async Task CheckPackageAndTransientsAsync_ShortVersion_MatchesNormalizedVersion()
    {
        var source = new FakeMetadataSource().WithPackage("A", "4.0.0");

        var results = await CreateNugetCheck(source).CheckPackageAndTransientsAsync("A", "4.0");

        Assert.That(results.Warnings, Is.Empty);
        Assert.That(results.NugetPackageOriginalVersion, Is.EqualTo("4.0.0"));
    }

    [TestCase("[1.0.0]", "1.0.0")]
    [TestCase("1.*", "1.1.0")]
    [TestCase("[1.0.0, 2.0.0)", "1.0.0")]
    public async Task CheckPackageAndTransientsAsync_RootVersionRangeOrFloat_ResolvesLikeNuGet(string requested, string expected)
    {
        var source = new FakeMetadataSource()
            .WithPackage("A", "1.0.0")
            .WithPackage("A", "1.1.0")
            .WithPackage("A", "2.0.0");

        var results = await CreateNugetCheck(source).CheckPackageAndTransientsAsync("A", requested);

        Assert.That(results.NugetPackageOriginalVersion, Is.EqualTo(expected));
    }

    [Test]
    public async Task CheckPackageAndTransientsAsync_FlaggedPackage_ReturnsAllWarnings()
    {
        var source = new FakeMetadataSource().WithPackage("A", "1.0.0", vulnerable: true, deprecated: true, listed: false);

        var results = await CreateNugetCheck(source).CheckPackageAndTransientsAsync("A", "1.0.0");

        Assert.That(Messages(results), Is.EqualTo(new[] { WarningMessages.NotListed, WarningMessages.Vulnerable, WarningMessages.Deprecated }));
        Assert.That(BreadCrumbs(results), Is.All.EqualTo("A 1.0.0"));
    }

    [TestCase("B [1.0.0, )", "B 1.0.0")]         // lowest applicable version
    [TestCase("B (1.0.0, )", "B 1.1.0")]         // exclusive lower bound
    [TestCase("B [0.5.0, )", "B 1.0.0")]         // minimum does not exist -> next one
    [TestCase("B [1.1.0, 2.0.0)", "B 1.1.0")]    // upper bound
    [TestCase("B 1.1", "B 1.1.0")]               // bare version means ">= 1.1"
    public async Task CheckPackageAndTransientsAsync_DependencyRange_ResolvesLowestApplicableVersion(string dependency, string expectedPackage)
    {
        var source = new FakeMetadataSource()
            .WithPackage("A", "1.0.0", dependencies: [dependency])
            .WithPackage("B", "1.0.0", vulnerable: true)
            .WithPackage("B", "1.1.0", vulnerable: true)
            .WithPackage("B", "2.0.0", vulnerable: true);

        var results = await CreateNugetCheck(source).CheckPackageAndTransientsAsync("A", "1.0.0");

        Assert.That(BreadCrumbs(results), Is.EqualTo(new[] { $"A 1.0.0 > {expectedPackage}" }));
    }

    [Test]
    public async Task CheckPackageAndTransientsAsync_DependencyRangeWithoutMatch_ReturnsVersionNotFound()
    {
        var source = new FakeMetadataSource()
            .WithPackage("A", "1.0.0", dependencies: ["B [3.0.0, )"])
            .WithPackage("B", "1.0.0");

        var results = await CreateNugetCheck(source).CheckPackageAndTransientsAsync("A", "1.0.0");

        Assert.That(Messages(results), Is.EqualTo(new[] { WarningMessages.VersionNotFound }));
        Assert.That(BreadCrumbs(results), Is.EqualTo(new[] { "A 1.0.0 > B [3.0.0, )" }));
    }

    [Test]
    public async Task CheckPackageAndTransientsAsync_UnknownDependency_ReturnsPackageNotFound()
    {
        var source = new FakeMetadataSource().WithPackage("A", "1.0.0", dependencies: ["Missing [1.0.0, )"]);

        var results = await CreateNugetCheck(source).CheckPackageAndTransientsAsync("A", "1.0.0");

        Assert.That(Messages(results), Is.EqualTo(new[] { WarningMessages.PackageNotFound }));
        Assert.That(BreadCrumbs(results), Is.EqualTo(new[] { "A 1.0.0 > Missing [1.0.0, )" }));
    }

    [Test]
    public async Task CheckPackageAndTransientsAsync_PrereleaseDependency_ResolvesPrereleaseVersion()
    {
        var source = new FakeMetadataSource()
            .WithPackage("A", "1.0.0", dependencies: ["B [1.0.0-beta.1, )"])
            .WithPackage("B", "1.0.0-beta.1", vulnerable: true);

        var results = await CreateNugetCheck(source).CheckPackageAndTransientsAsync("A", "1.0.0");

        Assert.That(BreadCrumbs(results), Is.EqualTo(new[] { "A 1.0.0 > B 1.0.0-beta.1" }));
    }

    [Test]
    public async Task CheckPackageAndTransientsAsync_NestedDependency_ReturnsFullBreadCrumb()
    {
        var source = new FakeMetadataSource()
            .WithPackage("A", "1.0.0", dependencies: ["B [1.0.0, )"])
            .WithPackage("B", "1.0.0", dependencies: ["C [1.0.0, )"])
            .WithPackage("C", "1.0.0", vulnerable: true);

        var results = await CreateNugetCheck(source).CheckPackageAndTransientsAsync("A", "1.0.0");

        Assert.That(BreadCrumbs(results), Is.EqualTo(new[] { "A 1.0.0 > B 1.0.0 > C 1.0.0" }));
    }

    [Test]
    public async Task CheckPackageAndTransientsAsync_CyclicDependencies_Terminates()
    {
        var source = new FakeMetadataSource()
            .WithPackage("A", "1.0.0", dependencies: ["B [1.0.0, )"])
            .WithPackage("B", "1.0.0", dependencies: ["A [1.0.0, )"]);

        var results = await CreateNugetCheck(source).CheckPackageAndTransientsAsync("A", "1.0.0");

        Assert.That(results.Warnings, Is.Empty);
    }

    [Test]
    public async Task CheckPackageAndTransientsAsync_SharedDependency_ReportedOnce()
    {
        var source = new FakeMetadataSource()
            .WithPackage("A", "1.0.0", dependencies: ["B [1.0.0, )", "C [1.0.0, )"])
            .WithPackage("B", "1.0.0", dependencies: ["D [1.0.0, )"])
            .WithPackage("C", "1.0.0", dependencies: ["D [1.0.0, )"])
            .WithPackage("D", "1.0.0", vulnerable: true);

        var results = await CreateNugetCheck(source).CheckPackageAndTransientsAsync("A", "1.0.0");

        Assert.That(BreadCrumbs(results), Is.EqualTo(new[] { "A 1.0.0 > B 1.0.0 > D 1.0.0" }));
    }

    [Test]
    public async Task CheckPackageAndTransientsAsync_SecondRootPackage_ReportsSharedDependencyAgain()
    {
        var source = new FakeMetadataSource()
            .WithPackage("R1", "1.0.0", dependencies: ["D [1.0.0, )"])
            .WithPackage("R2", "1.0.0", dependencies: ["D [1.0.0, )"])
            .WithPackage("D", "1.0.0", vulnerable: true);
        var nugetCheck = CreateNugetCheck(source);

        var first = await nugetCheck.CheckPackageAndTransientsAsync("R1", "1.0.0");
        var second = await nugetCheck.CheckPackageAndTransientsAsync("R2", "1.0.0");

        Assert.That(BreadCrumbs(first), Is.EqualTo(new[] { "R1 1.0.0 > D 1.0.0" }));
        Assert.That(BreadCrumbs(second), Is.EqualTo(new[] { "R2 1.0.0 > D 1.0.0" }));
    }

    [Test]
    public async Task CheckPackageAndTransientsAsync_MultipleDependencyGroups_ScansEveryGroup()
    {
        var source = new FakeMetadataSource()
            .WithPackage("A", "1.0.0", dependencies: ["B [1.0.0, )"])
            .WithDependencyGroup("A", "1.0.0", "netstandard2.0", "C [1.0.0, )")
            .WithPackage("B", "1.0.0")
            .WithPackage("C", "1.0.0", vulnerable: true);

        var results = await CreateNugetCheck(source).CheckPackageAndTransientsAsync("A", "1.0.0");

        Assert.That(BreadCrumbs(results), Is.EqualTo(new[] { "A 1.0.0 > C 1.0.0" }));
    }

    [Test]
    public async Task CheckPackageAndTransientsAsync_WithProgress_ReportsEveryScannedPackage()
    {
        var source = new FakeMetadataSource()
            .WithPackage("A", "1.0.0", dependencies: ["B [1.0.0, )"])
            .WithPackage("B", "1.0.0");
        var progress = new RecordingProgress();

        await CreateNugetCheck(source, progress).CheckPackageAndTransientsAsync("A", "1.0.0");

        Assert.That(progress.Messages, Is.EqualTo(new[] { "Scanning A 1.0.0", "Scanning B 1.0.0" }));
    }

    [Test]
    public async Task IsVulnerableAsync_ShortVersion_MatchesNormalizedVersion()
    {
        var source = new FakeMetadataSource().WithPackage("A", "4.0.0", vulnerable: true);

        Assert.That(await CreateNugetCheck(source).IsVulnerableAsync("A", "4.0"), Is.True);
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task IsDeprecatedAsync_KnownVersion_ReturnsFlag(bool deprecated)
    {
        var source = new FakeMetadataSource().WithPackage("A", "1.0.0", deprecated: deprecated);

        Assert.That(await CreateNugetCheck(source).IsDeprecatedAsync("A", "1.0.0"), Is.EqualTo(deprecated));
    }

    [Test]
    public async Task IsListedAsync_UnknownVersion_ReturnsNull()
    {
        var source = new FakeMetadataSource().WithPackage("A", "1.0.0");

        Assert.That(await CreateNugetCheck(source).IsListedAsync("A", "9.9.9"), Is.Null);
    }

    [Test]
    public async Task IsDeprecatedAsync_UnknownPackage_ReturnsNull()
    {
        Assert.That(await CreateNugetCheck(new FakeMetadataSource()).IsDeprecatedAsync("Missing", "1.0.0"), Is.Null);
    }
}
