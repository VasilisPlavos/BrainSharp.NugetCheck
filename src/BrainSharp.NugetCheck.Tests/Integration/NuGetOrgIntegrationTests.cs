using BrainSharp.NugetCheck.Services;
using BrainSharp.NugetCheck.Tests.Fakes;

namespace BrainSharp.NugetCheck.Tests.Integration;

// Talks to the real nuget.org. Assert only facts that do not change over time — never warning counts.
[Category("Integration")]
public class NuGetOrgIntegrationTests
{
    private string _cacheDirectory = null!;
    private NugetCheck _nugetCheck = null!;

    [SetUp]
    public void SetUp()
    {
        _cacheDirectory = TestDirectories.Create();
        _nugetCheck = new NugetCheck(cache: new FilePackageCache(_cacheDirectory));
    }

    [TearDown]
    public void TearDown() => TestDirectories.Delete(_cacheDirectory);

    [TestCase("Newtonsoft.Json", "12.0.3", true)]
    [TestCase("Newtonsoft.Json", "13.0.3", false)]
    public async Task IsVulnerableAsync_KnownVersion_ReturnsExpected(string packageName, string packageVersion, bool expected)
    {
        Assert.That(await _nugetCheck.IsVulnerableAsync(packageName, packageVersion), Is.EqualTo(expected));
    }

    [TestCase("BrainSharp.Xml", "1.0.2", true)]
    [TestCase("BrainSharp.Xml", "1.0.6", false)]
    public async Task IsDeprecatedAsync_KnownVersion_ReturnsExpected(string packageName, string packageVersion, bool expected)
    {
        Assert.That(await _nugetCheck.IsDeprecatedAsync(packageName, packageVersion), Is.EqualTo(expected));
    }

    [TestCase("BrainSharp.Xml", "1.0.1", false)]
    [TestCase("BrainSharp.Xml", "1.0.6", true)]
    public async Task IsListedAsync_KnownVersion_ReturnsExpected(string packageName, string packageVersion, bool expected)
    {
        Assert.That(await _nugetCheck.IsListedAsync(packageName, packageVersion), Is.EqualTo(expected));
    }

    [Test]
    public async Task SearchPackageAsync_UnknownPackage_ReturnsNull()
    {
        Assert.That(await _nugetCheck.SearchPackageAsync("brainsharp-nugetcheck-package-that-does-not-exist"), Is.Null);
    }

    [Test]
    public async Task SearchPackageAsync_RealPackage_IsReadableFromFileCache()
    {
        await _nugetCheck.SearchPackageAsync("Microsoft.NET.Test.Sdk");

        var cached = await new FilePackageCache(_cacheDirectory).GetAsync("microsoft.net.test.sdk");

        Assert.That(cached, Is.Not.Null);
        Assert.That(cached!.PackageMetadataRegistrations.SelectMany(x => x.DependencySets), Is.Not.Empty);
    }

    [Test]
    public async Task CheckPackageAndTransientsAsync_VulnerableProjectFile_ReturnsWarnings()
    {
        var filePath = Path.Combine(AppContext.BaseDirectory, "Files", "Vulnerable.csproj");

        var results = await _nugetCheck.CheckPackageAndTransientsAsync(filePath);

        Assert.That(results.PackageReferences, Has.Count.EqualTo(2));
        Assert.That(results.TotalWarnings, Is.GreaterThanOrEqualTo(2));
    }
}
