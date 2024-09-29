namespace BrainSharp.NugetCheck.Entities;

public class NugetPackage2
{

    public class Rootobject
    {
        public DateTime DateScanned { get; set; }
        public string NugetPackageId { get; set; }
        public Packagemetadataregistration[] PackageMetadataRegistrations { get; set; }
    }

    public class Packagemetadataregistration
    {
        public Identity Identity { get; set; }
        public string OriginalVersion { get; set; }
        public Dependencyset[] DependencySets { get; set; }
        public Deprecationmetadata DeprecationMetadata { get; set; }
        public bool IsListed { get; set; }
        public object Vulnerabilities { get; set; }
    }

    public class Identity
    {
        public string Id { get; set; }
        public string Version { get; set; }
    }

    public class Deprecationmetadata
    {
        public string message { get; set; }
        public string[] reasons { get; set; }
        public object alternatePackage { get; set; }
    }

    public class Dependencyset
    {
        public Targetframework TargetFramework { get; set; }
        public Package[] Packages { get; set; }
    }

    public class Targetframework
    {
        public string Framework { get; set; }
        public string Version { get; set; }
        public string Platform { get; set; }
        public string PlatformVersion { get; set; }
        public bool HasPlatform { get; set; }
        public bool HasProfile { get; set; }
        public string Profile { get; set; }
        public string DotNetFrameworkName { get; set; }
        public string DotNetPlatformName { get; set; }
        public bool IsPCL { get; set; }
        public bool IsPackageBased { get; set; }
        public bool AllFrameworkVersions { get; set; }
        public bool IsUnsupported { get; set; }
        public bool IsAgnostic { get; set; }
        public bool IsAny { get; set; }
        public bool IsSpecificFramework { get; set; }
    }

    public class Package
    {
        public string Id { get; set; }
        public object[] Include { get; set; }
        public object[] Exclude { get; set; }
        public Versionrange VersionRange { get; set; }
    }

    public class Versionrange
    {
        public bool IsFloating { get; set; }
        public string MinVersion { get; set; }
        public object MaxVersion { get; set; }
        public bool HasLowerBound { get; set; }
        public bool IsMinInclusive { get; set; }
        public bool HasUpperBound { get; set; }
        public bool IsMaxInclusive { get; set; }
        public bool HasLowerAndUpperBounds { get; set; }
        public object Float { get; set; }
        public string OriginalString { get; set; }
    }

}