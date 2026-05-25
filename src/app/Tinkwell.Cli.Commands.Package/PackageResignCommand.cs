using System.ComponentModel;
using Spectre.Console.Cli;
using Tinkwell.Cli;
using Tinkwell.Cli.Commands;
using Tinkwell.Package;

namespace Tinkwell.Cli.Commands.Package;

public sealed class PackageResignSettings : TwSettings
{
    [Description("Source package zip file")]
    [CommandArgument(0, "<input>")]
    public string Input { get; set; } = "";

    [Description("Output package zip file")]
    [CommandArgument(1, "<output>")]
    public string Output { get; set; } = "";

    [Description("Path to the new PKCS#8 private key file")]
    [CommandOption("--key|-k")]
    public string KeyFile { get; set; } = "";
}

[CliCommand("package", "resign", Description = "Re-sign a Tinkwell package with a new key")]
public sealed class PackageResignCommand : AsyncCommand<PackageResignSettings>
{
    public override async Task<int> ExecuteAsync(
        CommandContext context, PackageResignSettings settings, CancellationToken ct)
    {
        var output = new OutputContext(settings);

        try
        {
            if (string.IsNullOrEmpty(settings.KeyFile))
                throw new TwCommandException("Private key required. Use --key.");

            var privateKey = await File.ReadAllBytesAsync(settings.KeyFile, ct);

            var packer = new TwPackage();
            await output.RunWithStatusAsync("Re-signing...", () =>
                packer.ResignAsync(settings.Input, settings.Output, privateKey, ct));

            output.WriteSuccess($"Package re-signed: [bold]{settings.Output}[/]");
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
}
