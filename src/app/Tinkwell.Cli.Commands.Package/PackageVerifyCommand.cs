using System.ComponentModel;
using Spectre.Console.Cli;
using Tinkwell.Cli;
using Tinkwell.Cli.Commands;
using Tinkwell.Package;

namespace Tinkwell.Cli.Commands.Package;

public sealed class PackageVerifySettings : TwSettings
{
    [Description("Package path: zip file, directory, or package.tw")]
    [CommandArgument(0, "<path>")]
    public string Path { get; set; } = "";

    [Description("Path to a publisher public key file. May be specified multiple times to trust several publishers.")]
    [CommandOption("--key|-k")]
    public string[] KeyFiles { get; set; } = Array.Empty<string>();

    [Description("Allow packages without signatures")]
    [CommandOption("--allow-unsigned")]
    [DefaultValue(false)]
    public bool AllowUnsigned { get; set; }

    [Description("Accept integrity-only verification when no --key is supplied. Insecure: cannot detect forged signature manifests.")]
    [CommandOption("--allow-integrity-only")]
    [DefaultValue(false)]
    public bool AllowIntegrityOnly { get; set; }
}

[CliCommand("package", "verify", Description = "Verify a Tinkwell package integrity and signatures")]
public sealed class PackageVerifyCommand : AsyncCommand<PackageVerifySettings>
{
    public override async Task<int> ExecuteAsync(
        CommandContext context, PackageVerifySettings settings, CancellationToken ct)
    {
        var output = new OutputContext(settings);

        try
        {
            var trustedKeys = new List<byte[]>(settings.KeyFiles.Length);
            foreach (var keyFile in settings.KeyFiles)
                trustedKeys.Add(await File.ReadAllBytesAsync(keyFile, ct));

            if (trustedKeys.Count == 0 && !settings.AllowUnsigned && !settings.AllowIntegrityOnly)
            {
                output.WriteError(
                    "No publisher key supplied. Pass --key <path> (once per trusted publisher), " +
                    "or --allow-integrity-only to verify without checking the signature, " +
                    "or --allow-unsigned for unsigned packages.");
                return 1;
            }

            var options = new VerifyOptions
            {
                TrustedKeys = trustedKeys,
                RequireSignatures = !settings.AllowUnsigned,
                AllowIntegrityOnly = settings.AllowIntegrityOnly,
            };

            var packer = new TwPackage();
            var result = await output.RunWithStatusAsync("Verifying...", () =>
                packer.VerifyAsync(settings.Path, options, ct));

            if (result.IsValid)
            {
                output.WriteSuccess("Package is valid");
            }
            else
            {
                output.WriteError("Package verification failed");
            }

            foreach (var issue in result.Issues)
            {
                var severity = issue.Severity == VerificationSeverity.Error ? "red" : "yellow";
                var label = issue.Severity == VerificationSeverity.Error ? "ERROR" : "WARN";
                output.WriteMarkup(
                    $"  [{severity}]{label}[/] [{severity}]{issue.Code}[/]: {Spectre.Console.Markup.Escape(issue.Message)}");
            }

            return result.IsValid ? 0 : 1;
        }
        catch (PackageException ex)
        {
            output.WriteError(ex.Message);
            return 1;
        }
    }
}
