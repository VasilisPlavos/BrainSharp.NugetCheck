using System.Xml.Linq;
using BrainSharp.NugetCheck.Dtos;

namespace BrainSharp.NugetCheck;

/// <summary>
/// Central Package Management: the PackageVersion and GlobalPackageReference items of the nearest Directory.Packages.props.
/// Imports inside the props file are not followed.
/// </summary>
internal class CentralPackageVersions
{
    private const string FileName = "Directory.Packages.props";

    /// <summary>PackageVersion items by package id; the last definition wins and conditions are ignored.</summary>
    public Dictionary<string, string> Versions { get; } = new(StringComparer.OrdinalIgnoreCase);

    public List<PackageDto> GlobalPackageReferences { get; } = [];

    /// <summary>ManagePackageVersionsCentrally as set in the props file, or null.</summary>
    public bool? Enabled { get; private set; }

    /// <summary>Finds the nearest Directory.Packages.props from the project folder upwards, like MSBuild does.</summary>
    public static CentralPackageVersions? Find(string projectFilePath)
    {
        for (var directory = Path.GetDirectoryName(Path.GetFullPath(projectFilePath)); directory != null; directory = Path.GetDirectoryName(directory))
        {
            var path = Path.Combine(directory, FileName);
            if (File.Exists(path)) return Read(XDocument.Load(path));
        }

        return null;
    }

    internal static CentralPackageVersions Read(XDocument props)
    {
        var central = new CentralPackageVersions { Enabled = ReadManagePackageVersionsCentrally(props) };

        foreach (var item in ProjectXml.Items(props, "PackageVersion"))
        {
            if (item.Version != null) central.Versions[item.Id] = item.Version;
        }

        central.GlobalPackageReferences.AddRange(ProjectXml.Items(props, "GlobalPackageReference")
            .Select(item => new PackageDto { NugetPackageId = item.Id, Version = item.Version }));
        return central;
    }

    /// <summary>The last ManagePackageVersionsCentrally of any PropertyGroup, ignoring conditions; null when not set.</summary>
    internal static bool? ReadManagePackageVersionsCentrally(XDocument document)
    {
        var value = document.Descendants()
            .LastOrDefault(element => element.Name.LocalName == "ManagePackageVersionsCentrally" && element.Parent?.Name.LocalName == "PropertyGroup")
            ?.Value.Trim();
        return bool.TryParse(value, out var enabled) ? enabled : null;
    }
}
