using System.Text.Json.Serialization;

namespace Tinkwell.Cli.Commands.Templates.Manifest;

sealed class TemplateManifest
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("hidden")]
    public bool Hidden { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; } = "standard";

    [JsonPropertyName("questions")]
    public List<Question> Questions { get; set; } = new();

    [JsonPropertyName("sequence")]
    public List<SequenceStep> Sequence { get; set; } = new();

    [JsonPropertyName("files")]
    public List<TemplateFile> Files { get; set; } = new();
}
