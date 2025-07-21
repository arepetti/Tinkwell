using Spectre.Console;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Tinkwell.Bootstrapper;

namespace Tinkwell.Cli.Commands.Templates.Manifest;

sealed class TemplateManifest
{
    public static readonly string FileName = "template.json";
    public static readonly string PublicRepositoryUrl = "https://github.com/arepetti/tinkwell-dx";
    public static readonly string PublicRepositoryContentUrl = "https://raw.githubusercontent.com/arepetti/Tinkwell-DX/refs/heads/master/Templates";

    public static async Task DownloadRemoteTemplatesAsync()
    {
        try
        {
            AnsiConsole.MarkupLineInterpolated($"Fetching templates from [blue]{PublicRepositoryUrl}[/]...");
            using var httpClient = new HttpClient();

            var targetDirectory = TemplatesFromRegistry;
            string templateListLocalPath = Path.Combine(targetDirectory, "registry.ini");
            await DownloadFileFromRepository(httpClient, "registry.ini", templateListLocalPath);

            foreach (var entry in await File.ReadAllLinesAsync(templateListLocalPath))
            {
                // Download the specified file
                string fileName = entry.Split('=')[1].Trim();
                string localFileName = Path.Combine(targetDirectory, fileName);
                await DownloadFileFromRepository(httpClient, fileName, localFileName);

                // If downloaded before then remove the old template
                string templateDirectoryName = Path.Combine(targetDirectory, Path.GetFileNameWithoutExtension(fileName));
                if (Directory.Exists(templateDirectoryName))
                    Directory.Delete(templateDirectoryName, true);

                // Extract the zip
                ZipFile.ExtractToDirectory(localFileName, targetDirectory);
                File.Delete(localFileName);
            }

            File.Delete(templateListLocalPath);
        }
        catch (Exception e)
        {
            AnsiConsole.MarkupLineInterpolated($"[red]Error downloading templates from remote registry: {e.Message}[/]");
        }
    }

    public static IEnumerable<string> GetSearchPaths(string path)
    {
        return [
            TemplatesFromRegistry,
            BuildPath(Environment.SpecialFolder.LocalApplicationData),
            BuildPath(Environment.SpecialFolder.UserProfile),
            Path.Combine(StrategyAssemblyLoader.GetEntryAssemblyDirectoryName(), "Templates"),
            string.IsNullOrWhiteSpace(path) ? "" : Path.GetFullPath(path),
        ];
    }

    public static async Task<IEnumerable<TemplateManifest>> FindAllAsync(string path, bool includeAll = false)
    {
        Directory.CreateDirectory(TemplatesFromRegistry);
        if (!Directory.EnumerateDirectories(TemplatesFromRegistry).Any())
            await DownloadRemoteTemplatesAsync();

        return FindAllImpl(path, includeAll);
    }

    public static TemplateManifest LoadFromId(string templatesDirectoryPath, string templateId)
    {
        var manifest = FindAllImpl(templatesDirectoryPath, true)
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

    private static string TemplatesFromRegistry
        => BuildPath(Environment.SpecialFolder.ApplicationData);

    private static string BuildPath(Environment.SpecialFolder folder)
        => Path.Combine(Environment.GetFolderPath(folder), "Tinkwell", "Templates");

    private static TemplateManifest? Load(string  path)
    {
        var manifest = JsonSerializer.Deserialize<TemplateManifest>(File.ReadAllText(path, Encoding.UTF8));
        if (manifest is not null)
            manifest.FullPath = path;

        return manifest;
    }

    private static IEnumerable<TemplateManifest> FindAllImpl(string path, bool includeAll)
    {
        var templatesPaths = GetSearchPaths(path)
            .Where(x => !string.IsNullOrWhiteSpace(x) && Directory.Exists(x))
            .Distinct()
            .ToArray();

        return templatesPaths
            .SelectMany(x => FindAllInFolder(x, includeAll))
            .OrderBy(x => x.Id);
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

    private static async Task DownloadFileFromRepository(HttpClient client, string remoteFileName, string localFilePath)
    {
        var url = $"{PublicRepositoryContentUrl}/{remoteFileName}";
        AnsiConsole.MarkupLineInterpolated($"Downloading [blue]{url}[/]...");
        using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);

        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync();
        using var fileStream = new FileStream(localFilePath, FileMode.Create, FileAccess.Write, FileShare.None);

        await stream.CopyToAsync(fileStream);
    }
}
