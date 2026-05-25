using System.ComponentModel;
using System.Text.Json;
using Spectre.Console;
using Spectre.Console.Cli;
using Tinkwell.Cli;
using Tinkwell.Cli.Commands;
using Tinkwell.Package;

namespace Tinkwell.Cli.Commands.Package;

public sealed class PackageCreateManifestSettings : TwSettings
{
    [Description("Output file path or directory (defaults to package.tw in current directory)")]
    [CommandArgument(0, "[output]")]
    public string? Output { get; set; }

    [Description("Set a property value (name=value). Repeatable. Skips interactive prompts.")]
    [CommandOption("--set|-s")]
    public string[]? Set { get; set; }
}

[CliCommand("package", "create-manifest", Description = "Create a package.tw manifest file")]
public sealed class PackageCreateManifestCommand : AsyncCommand<PackageCreateManifestSettings>
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public override async Task<int> ExecuteAsync(
        CommandContext context, PackageCreateManifestSettings settings, CancellationToken ct)
    {
        var output = new OutputContext(settings);

        try
        {
            var outputPath = ResolveOutputPath(settings.Output);
            var setValues = ParseSetValues(settings.Set);
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (setValues is not null)
            {
                foreach (var (k, v) in setValues)
                    if (!string.IsNullOrEmpty(v))
                        values[k] = v;
            }
            else if (output.NonInteractive)
            {
                ManifestPrompt.ReadFromStdin(values);
            }
            else
            {
                ManifestPrompt.PromptInteractively(values);
            }

            if (!values.TryGetValue("name", out var name) || string.IsNullOrWhiteSpace(name))
            {
                output.WriteError("Package name is required.");
                return 1;
            }

            var manifest = ManifestPrompt.BuildManifest(values);

            var dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            var text = ManifestFormat.Write(manifest);
            await File.WriteAllTextAsync(outputPath, text, ct);

            if (output.Format == OutputFormat.Jsonl)
            {
                var json = JsonSerializer.Serialize(
                    new { status = "ok", path = outputPath }, JsonOpts);
                Console.WriteLine(json);
            }
            else
            {
                output.WriteSuccess($"Manifest created: [bold]{Markup.Escape(outputPath)}[/]");
            }

            return 0;
        }
        catch (TwCommandException ex)
        {
            output.WriteError(ex.Message);
            return 1;
        }
        catch (OutOfMemoryException) { Environment.FailFast("Out of memory"); throw; }
        catch (Exception ex)
        {
            output.WriteError(ex.Message);
            return 1;
        }
    }

    private static string ResolveOutputPath(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return Path.Combine(Directory.GetCurrentDirectory(), WellKnownPaths.Manifest);

        var full = Path.GetFullPath(raw);
        if (Directory.Exists(full))
            return Path.Combine(full, WellKnownPaths.Manifest);

        return full;
    }

    private static Dictionary<string, string>? ParseSetValues(string[]? entries)
    {
        if (entries is null || entries.Length == 0)
            return null;

        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in entries)
        {
            var eq = entry.IndexOf('=');
            if (eq <= 0)
                throw new TwCommandException($"Invalid --set value '{entry}'. Expected name=value.");

            var key = entry[..eq].Trim();
            var value = entry[(eq + 1)..].Trim();
            result[key] = value;
        }

        return result;
    }
}