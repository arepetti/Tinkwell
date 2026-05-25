using System.ComponentModel;
using Spectre.Console.Cli;
using Tinkwell.Cli;
using Tinkwell.Cli.Commands;
using Tinkwell.Package;

namespace Tinkwell.Cli.Commands.Identity;

public sealed class GenerateKeySettings : TwSettings
{
    [Description("Output path for the private key file (PKCS#8)")]
    [CommandArgument(0, "<private-key>")]
    public string PrivateKeyPath { get; set; } = "";

    [Description("Output path for the public key file (X.509 SubjectPublicKeyInfo)")]
    [CommandArgument(1, "<public-key>")]
    public string PublicKeyPath { get; set; } = "";

    [Description("Overwrite existing key files without prompting")]
    [CommandOption("--force")]
    [DefaultValue(false)]
    public bool Force { get; set; }
}

[CliCommand("identity", "generate-key", Description = "Generate an ECDSA P-384 key pair for signing and identity")]
public sealed class GenerateKeyCommand : AsyncCommand<GenerateKeySettings>
{
    public override async Task<int> ExecuteAsync(
        CommandContext context, GenerateKeySettings settings, CancellationToken ct)
    {
        var output = new OutputContext(settings);

        try
        {
            if (!settings.Force)
            {
                if (File.Exists(settings.PrivateKeyPath))
                    throw new TwCommandException(
                        $"Private key file already exists: {settings.PrivateKeyPath}. Use --force to overwrite.");

                if (File.Exists(settings.PublicKeyPath))
                    throw new TwCommandException(
                        $"Public key file already exists: {settings.PublicKeyPath}. Use --force to overwrite.");
            }

            var (privateKey, publicKey) = PackageSigner.GenerateKeyPair();
            var keyId = PackageSigner.ComputeKeyId(publicKey);

            await File.WriteAllBytesAsync(settings.PrivateKeyPath, privateKey, ct);
            await File.WriteAllBytesAsync(settings.PublicKeyPath, publicKey, ct);

            output.WriteSuccess($"Key pair generated ([dim]{keyId}[/])");
            output.WriteMarkup($"  Private key: [bold]{settings.PrivateKeyPath}[/]");
            output.WriteMarkup($"  Public key:  [bold]{settings.PublicKeyPath}[/]");
            return 0;
        }
        catch (TwCommandException)
        {
            throw;
        }
        catch (OutOfMemoryException) { Environment.FailFast("Out of memory"); throw; }
        catch (Exception ex)
        {
            output.WriteError(ex.Message);
            return 1;
        }
    }
}