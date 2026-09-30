namespace BrainSharp.NugetCheck.Services;

/// <summary>The package could not be loaded: it has no fresh cache entry and the package source did not answer.</summary>
public class PackageSourceUnavailableException(string packageId, Exception? innerException = null)
    : Exception($"{packageId} could not be loaded from the package source.", innerException)
{
    public string PackageId { get; } = packageId;
}
