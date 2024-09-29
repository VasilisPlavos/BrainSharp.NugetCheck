// Methodname_Condition_Expectation

using BrainSharp.NugetCheck.Services;

namespace BrainSharp.NugetCheck.Tests
{
    public class Tests
    {
        [SetUp]
        public void Setup()
        {
            var deleteLocalStorage = false;
            if (deleteLocalStorage) LocalStorageService.DeleteStorage();
        }

        [Test]
        [TestCase("BrainSharp.Xml", "1.0.6", 0)]
        [TestCase("BrainSharp.Xml", "1.0.7", 1)]
        [TestCase("coverlet.collector", "3.1.2", 0)]
        [TestCase("Microsoft.NET.Test.Sdk", "17.3.2", 20)]  // this can take 30+ seconds
        [TestCase("MSTest.TestAdapter", "2.2.10", 3)]       // this can take 30+ seconds
        [TestCase("MSTest.TestFramework", "2.2.10", 2)]     // this can take 30+ seconds
        public async Task CheckPackageAndTransients_StringContents_ExpectedResult(string packageName, string packageVersion, int expectedWarnings)
        {
            var nugetCheck = new NugetCheck();
            var result = await nugetCheck.CheckPackageAndTransientsAsync(packageName, packageVersion);

            Assert.That(result, Is.Not.Null);
            Assert.That(result.NugetPackageId, Is.EqualTo(packageName));

            Assert.That(result.NugetPackageOriginalVersion!, Is.EqualTo(packageVersion));
            Assert.That(result.Warnings.Count, Is.EqualTo(expectedWarnings));
            Assert.Pass();
        }

        [Test]
        [TestCase("Test.csproj", 5, 3)]
        [TestCase("Test2.csproj", 5, 2)]
        public async Task CheckPackageAndTransientsAsync_GiveCsProjFile_ReturnResults(string fileName, int numberOfPackageReferences, int expectedWarnings)
        {
            var directory = Path.Combine(AppContext.BaseDirectory, "Files");
            var filePath = Path.Combine(directory, fileName);

            //var assemblies = AppDomain.CurrentDomain.GetAssemblies().ToList();
            var nugetCheck = new NugetCheck();
            var projectResults = await nugetCheck.CheckPackageAndTransientsAsync(filePath);

            Assert.That(projectResults, Is.Not.Null);
            Assert.That(projectResults.PackageReferences.Count, Is.EqualTo(numberOfPackageReferences));
            Assert.That(projectResults.TotalWarnings, Is.EqualTo(expectedWarnings));
        }

        [Test]
        [TestCase("sixlabors.imagesharp", "3.1.3", true)]
        [TestCase("sixlabors.imagesharp", "3.1.4", false)]
        [TestCase("Newtonsoft.Json", "12.0.3", true)]
        [TestCase("Newtonsoft.Json", "13.0.3", false)]
        [TestCase("System.ServiceModel.Duplex", "4.0.0", true)] // moderate severity
        [TestCase("System.ServiceModel.Duplex", "4.4.0", true)] // high severity
        public async Task IsVulnerable_StringContents_ExpectedResult(string packageName, string packageVersion, bool expected)
        {
            var nugetCheck = new NugetCheck();
            var isVulnerable = await nugetCheck.IsVulnerableAsync(packageName, packageVersion);

            Assert.That(isVulnerable, Is.Not.Null);
            Assert.That(isVulnerable, Is.EqualTo(expected));

            Assert.Pass();
        }

        [Test]
        [TestCase("BrainSharp.Xml", "1.0.2", true)]
        [TestCase("BrainSharp.Xml", "1.0.6", false)]
        public async Task IsDeprecated_StringContents_ExpectedResult(string packageName, string packageVersion, bool expected)
        {
            var nugetCheck = new NugetCheck();
            var isDeprecated = await nugetCheck.IsDeprecatedAsync(packageName, packageVersion);

            Assert.That(isDeprecated, Is.Not.Null);
            Assert.That(isDeprecated, Is.EqualTo(expected));

            Assert.Pass();
        }

        [Test]
        [TestCase("BrainSharp.Xml", "1.0.1", false)]
        [TestCase("BrainSharp.Xml", "1.0.3", false)]
        [TestCase("BrainSharp.Xml", "1.0.6", true)]
        public async Task IsListed_StringContents_ExpectedResult(string packageName, string packageVersion, bool expected)
        {
            var nugetCheck = new NugetCheck();
            var isListed = await nugetCheck.IsListedAsync(packageName, packageVersion);

            Assert.That(isListed, Is.Not.Null);
            Assert.That(isListed, Is.EqualTo(expected));

            Assert.Pass();
        }

        [Test]
        [TestCase("sixlabors.imagesharp", true)]
        [TestCase("uiogdfgjkdf", false)]
        public async Task SearchPackageAsync_GiveName_Return(string packageName, bool exist)
        {
            var nugetCheck = new NugetCheck();
            var package = await nugetCheck.SearchPackageAsync(packageName);

            if (!exist)
            {
                Assert.That(package, Is.Null);
                return;
            }

            Assert.That(package, Is.Not.Null);
            Assert.That(package?.NugetPackageId.ToLower(), Is.EqualTo(packageName.ToLower()));
        }

        [Test]
        [TestCase("BrainSharp.Xml")]
        public async Task TestLocalStorage(string packageName)
        {
            var nugetCheck = new NugetCheck();
            var package = await nugetCheck.SearchPackageAsync(packageName);
            LocalStorageService.DeleteStorage();
            await LocalStorageService.SaveNugetPackageAsync(package!);
            var package2 = await LocalStorageService.GetNugetPackageAsync2(packageName);
            var package3 = await LocalStorageService.GetNugetPackageAsync(packageName);
            Assert.That(package3, Is.Not.Null);
        }

        [Test]
        [TestCase("sixlabors.imagesharp", "3.1.3")]
        public async Task SearchPackageWithVersionAsync_GiveName_Return(string packageName, string packageVersion)
        {
            var nugetCheck = new NugetCheck();
            var package = await nugetCheck.SearchPackageAsync(packageName);
            Assert.That(package, Is.Not.Null);

            var packageVersionInfo = nugetCheck.SearchPackageVersionInfo(package, packageVersion);

            Assert.That(packageVersionInfo, Is.Not.Null);
            Assert.That(packageVersionInfo?.OriginalVersion.ToString(), Is.EqualTo(packageVersion));
        }
    }

}