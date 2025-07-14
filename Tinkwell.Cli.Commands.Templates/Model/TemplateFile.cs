using System.Text.Json.Serialization;

namespace Tinkwell.Cli.Commands.Templates.Manifest;

/// <summary>
/// Represents a single file inside the template, it'll be copied/appended
/// to create the desired output. The manifest is <see cref="TemplateManifest"/>.
/// </summary>
sealed class TemplateFile
{
    [JsonPropertyName("when")]
    public string When { get; set; } = "";

    [JsonPropertyName("original")]
    public string Original { get; set; } = "";

    [JsonPropertyName("target")]
    public string Target { get; set; } = "";

    [JsonPropertyName("mode")]
    public string Mode { get; set; } = "unspecified"; // "copy" or "append"
}
