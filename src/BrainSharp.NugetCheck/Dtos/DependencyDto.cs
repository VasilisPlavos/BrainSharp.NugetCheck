using System.Text.Json.Serialization;

namespace BrainSharp.NugetCheck.Dtos;

public class DependencyDto
{
    public string type { get; set; }
    [JsonPropertyName("id")]
    public string PackageId { get; set; }
    public string range { get; set; }
}