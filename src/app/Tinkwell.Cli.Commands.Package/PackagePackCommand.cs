using System.ComponentModel;
using Spectre.Console.Cli;
using Tinkwell.Cli;
using Tinkwell.Cli.Commands;
using Tinkwell.Package;

namespace Tinkwell.Cli.Commands.Package;

public sealed class PackagePackSettings : TwSettings
{
    [Description("Source directory (must contain package.tw and content/ unless --from-content is used)")]
    [CommandArgument(0, "<source>")]
    public string Source { get; set; } = ".";

    [Description("Output zip file path")]
    [CommandArgument(1, "<output>")]
    public string Output { get; set; } = "";

    [Description("Path to the PKCS#8 private key file for signing")]
    [CommandOption("--key|-k")]
    public string? KeyFile { get; set; }

    [Description("Skip signing")]
    [CommandOption("--no-sign")]
    [DefaultValue(false)]
    public bool NoSign { get; set; }

    [Description("Treat <source> as the content directory directly (no package.tw or content/ subfolder needed)")]
    [CommandOption("--from-content")]
    [DefaultValue(false)]
    public bool FromContent { get; set; }

    [Description("Path to a package.tw manifest file (used with --from-content)")]
    [CommandOption("--manifest|-m")]
    public string? ManifestFile { get; set; }
}

[CliCommand("package", "pack", Description = "Pack a directory into a Tinkwell package")]
public sealed class PackagePackCommand : AsyncCommand<PackagePackSettings>
{
    public override async Task<int> ExecuteAsync(
        CommandContext context, PackagePackSettings settings, CancellationToken ct)
    {
        var output = new OutputContext(settings);

        try
        {
            byte[]? privateKey = null;
            if (!settings.NoSign)
            {
                if (settings.KeyFile is null)
                    throw new TwCommandException(
                        "Private key required for signing. Use --key or --no-sign.");

                privateKey = await File.ReadAllBytesAsync(settings.KeyFile, ct);
            }

            var options = new PackOptions
            {
                PrivateKey = privateKey,
                Sign = !settings.NoSign,
            };

            var packer = new TwPackage();

            if (settings.FromContent)
            {
                var manifest = await ResolveManifestAsync(settings, output, ct);
                await output.RunWithStatusAsync("Packing...", () =>
                    packer.PackAsync(manifest, settings.Source, settings.Output, options, ct));
            }
            else
            {
                await output.RunWithStatusAsync("Packing...", () =>
                    packer.PackAsync(settings.Source, settings.Output, options, ct));
            }

            output.WriteSuccess($"Package created: [bold]{settings.Output}[/]");
            return 0;
        }
        catch (TwCommandException ex)
        {
            output.WriteError(ex.Message);
            return 1;
        }
        catch (PackageException ex)
        {
            output.WriteError(ex.Message);
            return 1;
        }
    }

    private static async Task<PackageManifest> ResolveManifestAsync(
        PackagePackSettings settings, OutputContext output, CancellationToken ct)
    {
        if (settings.ManifestFile is not null)
        {
            var text = await File.ReadAllTextAsync(settings.ManifestFile, ct);
            return ManifestFormat.Parse(text);
        }

        if (output.NonInteractive)
            throw new TwCommandException(
                "Manifest is required when using --from-content in non-interactive mode. " +
                "Use --manifest to specify a package.tw file.");

        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        ManifestPrompt.PromptInteractively(values);

        if (!values.TryGetValue("name", out var name) || string.IsNullOrWhiteSpace(name))
            throw new TwCommandException("Package name is required.");

        return ManifestPrompt.BuildManifest(values);
    }
}
