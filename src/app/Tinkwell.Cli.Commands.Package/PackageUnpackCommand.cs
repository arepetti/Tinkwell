using System.ComponentModel;
using Spectre.Console.Cli;
using Tinkwell.Cli;
using Tinkwell.Cli.Commands;
using Tinkwell.Package;

namespace Tinkwell.Cli.Commands.Package;

public sealed class PackageUnpackSettings : TwSettings
{
    [Description("Package zip file to unpack")]
    [CommandArgument(0, "<package>")]
    public string PackagePath { get; set; } = "";

    [Description("Output directory")]
    [CommandArgument(1, "<output>")]
    public string Output { get; set; } = "";

    [Description("Path to a publisher public key file. May be specified multiple times to trust several publishers.")]
    [CommandOption("--key|-k")]
    public string[] KeyFiles { get; set; } = Array.Empty<string>();

    [Description("Skip verification")]
    [CommandOption("--no-verify")]
    [DefaultValue(false)]
    public bool NoVerify { get; set; }

    [Description("Allow packages without signatures")]
    [CommandOption("--allow-unsigned")]
    [DefaultValue(false)]
    public bool AllowUnsigned { get; set; }

    [Description("Accept integrity-only verification when no --key is supplied. Insecure: cannot detect forged signature manifests.")]
    [CommandOption("--allow-integrity-only")]
    [DefaultValue(false)]
    public bool AllowIntegrityOnly { get; set; }
}

[CliCommand("package", "unpack", Description = "Unpack a Tinkwell package to a directory")]
public sealed class PackageUnpackCommand : AsyncCommand<PackageUnpackSettings>
{
    public override async Task<int> ExecuteAsync(
        CommandContext context, PackageUnpackSettings settings, CancellationToken ct)
    {
        var output = new OutputContext(settings);

        try
        {
            var trustedKeys = new List<byte[]>(settings.KeyFiles.Length);
            foreach (var keyFile in settings.KeyFiles)
                trustedKeys.Add(await File.ReadAllBytesAsync(keyFile, ct));

            var verify = !settings.NoVerify;
            if (verify && trustedKeys.Count == 0 && !settings.AllowUnsigned && !settings.AllowIntegrityOnly)
            {
                output.WriteError(
                    "No publisher key supplied. Pass --key <path> (once per trusted publisher), " +
                    "or --allow-integrity-only to verify without checking the signature, " +
                    "or --allow-unsigned for unsigned packages, or --no-verify to skip verification entirely.");
                return 1;
            }

            var options = new UnpackOptions
            {
                Verify = verify,
                TrustedKeys = trustedKeys,
                RequireSignatures = !settings.AllowUnsigned,
                AllowIntegrityOnly = settings.AllowIntegrityOnly,
            };

            var packer = new TwPackage();
            await output.RunWithStatusAsync("Unpacking...", () =>
                packer.UnpackAsync(settings.PackagePath, settings.Output, options, ct));

            output.WriteSuccess($"Package extracted to: [bold]{settings.Output}[/]");
            return 0;
        }
        catch (PackageException ex)
        {
            output.WriteError(ex.Message);
            return 1;
        }
    }
}
