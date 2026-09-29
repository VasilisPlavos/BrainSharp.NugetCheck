using BrainSharp.NugetCheck.Dtos;

namespace BrainSharp.NugetCheck.Entities;

public class Warning
{
    /// <summary>One of <see cref="WarningMessages"/>.</summary>
    public required string Message { get; set; }

    /// <summary>Path from the root package, e.g. "A 1.0.0 > B 2.0.0".</summary>
    public required string BreadCrumb { get; set; }

    public PackageMetadataRegistrationDto? Package { get; set; }
}
