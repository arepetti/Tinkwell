using System.Text.Json.Serialization;

namespace Tinkwell.Cli.Commands.Templates.Manifest;

sealed class SequenceStep
{
    [JsonPropertyName("templateId")]
    public string TemplateId { get; set; } = "";

    [JsonPropertyName("when")]
    public string? When { get; set; }
}
