using System.Text.Json;
using System.Text.Json.Serialization;

namespace Tinkwell.Cli.Commands.Templates.Manifest;

sealed class TemplateManifest
{
    public static readonly string FileName = "template.json";

    public static IEnumerable<TemplateManifest> FindAll(string path, bool showAll = false)
    {
        foreach (var directory in Directory.GetDirectories(path))
        {
            var manifestPath = Path.Combine(directory, FileName);
            if (File.Exists(manifestPath))
            {
                var manifest = JsonSerializer.Deserialize<TemplateManifest>(File.ReadAllText(manifestPath));
                if (manifest is not null && (!manifest.Hidden || showAll))
                    yield return manifest;
            }
        }
    }

    public static TemplateManifest LoadFromId(string templatesDirectoryPath, string templateId)
    {
        var templateDirectoryPath = Path.Combine(templatesDirectoryPath, templateId);
        if (!Directory.Exists(templateDirectoryPath))
            throw new DirectoryNotFoundException($"Template directory not found: {templateDirectoryPath}");

        var manifestPath = Path.Combine(templateDirectoryPath, FileName);
        if (!File.Exists(manifestPath))
            throw new FileNotFoundException($"Template manifest not found: {manifestPath}");

        var manifest = JsonSerializer.Deserialize<TemplateManifest>(File.ReadAllText(manifestPath));
        if (manifest is null)
            throw new InvalidOperationException($"Could not deserialize template manifest: {manifestPath}");

        return manifest;
    }

    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("author")]
    public string Author { get; set; } = "";

    [JsonPropertyName("copyright")]
    public string Copyright { get; set; } = "";

    [JsonPropertyName("website")]
    public string Webiste { get; set; } = "";

    [JsonPropertyName("version")]
    public string Version { get; set; } = "";

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
