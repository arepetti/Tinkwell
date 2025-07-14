using Spectre.Console;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Tinkwell.Bootstrapper;

namespace Tinkwell.Cli.Commands.Templates.Manifest;

sealed class TemplateManifest
{
    public static readonly string FileName = "template.json";

    public static IEnumerable<string> GetSearchPaths(string path)
    {
        return [
            BuildPath(Environment.SpecialFolder.ApplicationData),
            BuildPath(Environment.SpecialFolder.LocalApplicationData),
            BuildPath(Environment.SpecialFolder.UserProfile),
            Path.Combine(StrategyAssemblyLoader.GetEntryAssemblyDirectoryName(), "Templates"),
            string.IsNullOrWhiteSpace(path) ? "" : Path.GetFullPath(path),
        ];

        string BuildPath(Environment.SpecialFolder folder)
            => Path.Combine(Environment.GetFolderPath(folder), "Tinkwell", "Templates");
    }

    public static IEnumerable<TemplateManifest> FindAll(string path, bool includeAll = false)
    {
        var templatesPaths = GetSearchPaths(path)
            .Where(x => !string.IsNullOrWhiteSpace(x) && Directory.Exists(x))
            .Distinct()
            .ToArray();

        return templatesPaths
            .SelectMany(x => FindAllInFolder(x, includeAll))
            .OrderBy(x => x.Id);
    }

    public static TemplateManifest LoadFromId(string templatesDirectoryPath, string templateId)
    {
        var manifest = FindAll(templatesDirectoryPath, true)
            .FirstOrDefault(x => string.Equals(x.Id, templateId, StringComparison.Ordinal)); ;

        if (manifest is null)
            throw new InvalidOperationException($"Could not find or deserialize template {templateId}");

        return manifest;
    }

    public void Save(string templatesDirectoryPath)
    {
        if (string.IsNullOrWhiteSpace(templatesDirectoryPath))
            templatesDirectoryPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Tinkwell", "Templates");

        var templateDirectory = new DirectoryInfo(Path.Combine(templatesDirectoryPath, Id));
        if (!templateDirectory.Exists)
            templateDirectory.Create();

        string json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });

        FullPath = Path.Combine(templateDirectory.FullName, FileName);
        File.WriteAllText(FullPath, json, Encoding.UTF8);
    }

    [JsonIgnore]
    internal string? FullPath { get; set; }

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

    [JsonPropertyName("license")]
    public string License { get; set; } = "";

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

    private static TemplateManifest? Load(string  path)
    {
        var manifest = JsonSerializer.Deserialize<TemplateManifest>(File.ReadAllText(path, Encoding.UTF8));
        if (manifest is not null)
            manifest.FullPath = path;

        return manifest;
    }

    private static IEnumerable<TemplateManifest> FindAllInFolder(string path, bool includeAll)
    {
        var comparer = StrategyAssemblyLoader.ResolveStringComparisonForPath(path);
        foreach (var directory in Directory.GetDirectories(path))
        {
            var directoryName = Path.GetFileName(directory);
            var manifestPath = Path.Combine(directory, FileName);

            if (File.Exists(manifestPath))
            {
                var manifest = Load(manifestPath);
                if (manifest is null || (manifest.Hidden && !includeAll))
                    continue;

                // If the directory name does not match the template ID in the manifest then
                // we'd have problems later when processing the template, let's skip it right now.
                if (!string.Equals(manifest.Id, directoryName, comparer))
                {
                    AnsiConsole.MarkupLineInterpolated($"[red]Error[/]: mismatched directory name for [cyan]{manifest.Id}[/] ([blueviolet]{directoryName}/[/])");
                    continue;
                }

                yield return manifest;
            }
        }
    }
}
