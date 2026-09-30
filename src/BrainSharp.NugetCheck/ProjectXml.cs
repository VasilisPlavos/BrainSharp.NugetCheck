using System.Xml.Linq;

namespace BrainSharp.NugetCheck;

/// <summary>Reads MSBuild items without evaluating MSBuild: conditions are ignored and XML namespaces do not matter.</summary>
internal static class ProjectXml
{
    /// <summary>Every item named <paramref name="itemName"/> with an Include, from any ItemGroup; Version comes from the attribute or a child element.</summary>
    public static IEnumerable<(string Id, string? Version, string? VersionOverride)> Items(XDocument document, string itemName) =>
        document.Descendants()
            .Where(element => element.Name.LocalName == itemName && element.Parent?.Name.LocalName == "ItemGroup")
            .Select(element => (Id: ((string?)element.Attribute("Include"))?.Trim(),
                Version: Metadata(element, "Version"),
                VersionOverride: Metadata(element, "VersionOverride")))
            .Where(item => !string.IsNullOrEmpty(item.Id))
            .Select(item => (item.Id!, item.Version, item.VersionOverride));

    private static string? Metadata(XElement element, string name)
    {
        var value = (string?)element.Attribute(name) ?? element.Elements().FirstOrDefault(child => child.Name.LocalName == name)?.Value;
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
