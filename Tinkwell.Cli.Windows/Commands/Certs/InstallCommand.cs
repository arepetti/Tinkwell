using Spectre.Console.Cli;
using Spectre.Console;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using Tinkwell.Bootstrapper.Ipc;

namespace Tinkwell.Cli.Commands.Certs;

[CommandFor("install", parent: typeof(CertsCommand))]
[Description("Install a previously created self-signed certificate.")]
sealed class CreateCommand : Command<CreateCommand.Settings>
{
    public sealed class Settings : CertsCommand.Settings
    {
        [CommandArgument(0, "[PATH]")]
        [Description("Path of the certificate file to install.")]
        public string Path { get; set; } = $"./tw-{Environment.MachineName}.pfx";
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Consoles.Error.WriteLine("This command is supported only in Windows.");
            return ExitCode.Canceled;
        }

        string password = AnsiConsole.Ask<string>("Password?");

        string path = settings.Path;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(settings.Path))
        {
            Consoles.Error.MarkupLineInterpolated($"[red]Cannot find the certificate file[/] [blueviolet]{path}[/].");
            Consoles.Error.MarkupLineInterpolated($"Checking value of [blueviolet]{WellKnownNames.WebServerCertificatePath}[/].");
            path = Environment.GetEnvironmentVariable(WellKnownNames.WebServerCertificatePath) ?? "";
        }

        if (!File.Exists(path))
        {
            Consoles.Error.MarkupLineInterpolated($"[red]Cannot find the certificate file[/] [blueviolet]{path}[/].");
            return ExitCode.InvalidArgument;
        }

        AnsiConsole.MarkupLineInterpolated($"Installing [blueviolet]{path}[/]...");

        var cert = X509CertificateLoader.LoadPkcs12FromFile(path, password);
        var store = new X509Store(StoreName.Root, StoreLocation.CurrentUser);
        store.Open(OpenFlags.ReadWrite);
        store.Add(cert);
        store.Close();

        AnsiConsole.MarkupLineInterpolated($"Certificate [blueviolet]{path}[/] has been installed for current user");

        return ExitCode.Ok;
    }
}
