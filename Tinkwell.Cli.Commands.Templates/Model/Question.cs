using System.Text.Json.Serialization;

namespace Tinkwell.Cli.Commands.Templates.Manifest;

sealed class Question
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("when")]
    public string When { get; set; } = "";

    [JsonPropertyName("prompt")]
    public string Prompt { get; set; } = "";

    [JsonPropertyName("type")]
    public string Type { get; set; } = "text"; // confirm, selection

    [JsonPropertyName("default")]
    public string? Default { get; set; }

    [JsonPropertyName("options")]
    public List<string> Options { get; set; } = new();
}
