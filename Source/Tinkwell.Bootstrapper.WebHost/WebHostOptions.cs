using System.Globalization;
using Tinkwell.Bootstrapper.Ensamble;
using Tinkwell.Bootstrapper.Hosting;
using Tinkwell.Bootstrapper.Ipc;
using Tinkwell.Bootstrapper.Ipc.Extensions;

namespace Tinkwell.Bootstrapper.WebHost;

sealed class WebHostOptions
{
    public static async Task<WebHostOptions> LoadAsync()
    {
        var config = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        object? rootPath = null;
        object? requestPath = null;
        object? portNumber = null;
        if (HostingInformation.IsSupervised)
        {
            var pipeClient = new NamedPipeClient();
            var definition = await pipeClient.SendCommandToSupervisorAndDisconnectAsync<RunnerDefinition>(
                config, $"runners get --pid {Environment.ProcessId}", CancellationToken.None);

            if (definition.Children.Any())
                throw new InvalidOperationException($"{HostingInformation.RunnerName} cannot host firmlets, only one web application.");

            definition.Properties.TryGetValue("root_path", out rootPath);
            definition.Properties.TryGetValue("request_path", out requestPath);
            definition.Properties.TryGetValue("port_number", out portNumber);
        }

        var certPath = Environment.GetEnvironmentVariable(WellKnownNames.WebServerCertificatePath) ?? "";
        var certPassword = Environment.GetEnvironmentVariable(WellKnownNames.WebServerCertificatePass) ?? "";

        return new()
        {
            CertificatePath = string.IsNullOrWhiteSpace(certPath) ? "" : HostingInformation.GetFullPath(certPath),
            CertificatePassword = certPassword,
            WebRootPath = HostingInformation.GetFullPath(Convert.ToString(rootPath) ?? "./wwwroot"),
            RequestPathBase = Convert.ToString(requestPath) ?? "/",
            PortNumber = int.Parse(Convert.ToString(portNumber, CultureInfo.InvariantCulture) ?? "0", CultureInfo.InvariantCulture),
        };
    }

    public required string CertificatePath { get; init; }

    public required string CertificatePassword { get; init; }

    public required string WebRootPath { get; init; }

    public required string RequestPathBase { get; init; }

    public required int PortNumber { get; init; }
}