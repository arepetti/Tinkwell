using System.ComponentModel;
using System.Runtime.InteropServices;
using Spectre.Console;
using Spectre.Console.Cli;
using Tinkwell.Bootstrapper.Hosting;
using Tinkwell.Bootstrapper.Ipc;

namespace Tinkwell.Cli.Commands.Certs;

[CommandFor("create", parent: typeof(CertsCommand))]
[Description("Creates a new self-signed certificate.")]
sealed class CreateCommand : Command<CreateCommand.Settings>
{
    public sealed class Settings : CertsCommand.Settings
    {
        [CommandArgument(0, "[COMMON NAME]")]
        [Description("Name of the service to find.")]
        public string CommonName { get; set; } = $"Tinkwell for {Environment.MachineName}";

        [CommandOption("--validity")]
        [Description("Validity of the newly generated certificate (in years).")]
        public int Validity { get; set; } = 5;

        [CommandOption("--export-name")]
        [Description("Name, without extension, of the certificate file(s) to export.")]
        public string ExportFileName { get; set; } = $"tw-{Environment.MachineName}";

        [CommandOption("--export-path")]
        [Description("Path where the exported certificates should be saved, if omitted it's app data directory.")]
        public string ExportPath { get; set; } = HostingInformation.ApplicationDataDirectory;

        [CommandOption("--export-pem")]
        [Description("Export PEM certificate and key.")]
        public bool ExportPem { get; set; } = !RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

        [CommandOption("--set-environment")]
        [Description("Set the environment variables needed to run Tinkwell (Windows only).")]
        public bool SetEnvironmentVariable { get; set; }
            = string.IsNullOrEmpty(Environment.GetEnvironmentVariable(WellKnownNames.WebServerCertificatePath));

        [CommandOption("--unsafe-password")]
        public string Password { get; set; } = "";
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        string password = string.IsNullOrEmpty(settings.Password)
            ? AnsiConsole.Ask<string>("Password?")
            : settings.Password;

        var options = new SelfSignedCertificate.CreateOptions(settings.CommonName, settings.Validity, password);
        var certificate = SelfSignedCertificate.Create(options);
        AnsiConsole.MarkupLineInterpolated($"Created certificate [cyan]{settings.CommonName}[/]");

        SelfSignedCertificate.Export(
            certificate,
            password,
            Path.Combine(settings.ExportPath, settings.ExportFileName),
            settings.ExportPem,
            out var exportedFiles);

        if (settings.SetEnvironmentVariable)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Environment.SetEnvironmentVariable(
                    WellKnownNames.WebServerCertificatePath, exportedFiles[0], EnvironmentVariableTarget.User);

                Environment.SetEnvironmentVariable(
                    WellKnownNames.WebServerCertificatePass, password, EnvironmentVariableTarget.User);
            }
            else
            {
                Consoles.Error.MarkupLine("[red]--set-environment is supported only on Windows.[/]");
            }
        }

        foreach (string path in exportedFiles)
            AnsiConsole.MarkupLineInterpolated($"Exported [blueviolet]{path}[/]");

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[yellow]TO DO[/]");
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || !settings.SetEnvironmentVariable)
        {
            AnsiConsole.MarkupLineInterpolated($"Set an environment variable [blueviolet]{WellKnownNames.WebServerCertificatePath}[/] to [blueviolet]{exportedFiles[0]}[/].");
            AnsiConsole.MarkupLineInterpolated($"Set an environment variable [blueviolet]{WellKnownNames.WebServerCertificatePass}[/] to the password you specified.");
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            AnsiConsole.MarkupLine("Run [blueviolet]tw certs install[/] to install the certificate.");
        else
            AnsiConsole.MarkupLine("Manually install and trust the certificate.");

        return ExitCode.Ok;
    }
}
