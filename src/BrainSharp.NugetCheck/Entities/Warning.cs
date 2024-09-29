using BrainSharp.NugetCheck.Dtos;

namespace BrainSharp.NugetCheck.Entities;

public class Warning
{
    public string Message { get; set; }
    public string BreadCrumb { get; set; }
    public PackageMetadataRegistrationDto? Package { get; set; }
}