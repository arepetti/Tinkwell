using System.Globalization;
using Tinkwell.Bootstrapper.Hosting;
using Tinkwell.Bootstrapper.Ipc;
using Tinkwell.Bootstrapper.Ipc.Extensions;

namespace Tinkwell.Bootstrapper.GrpcHost;

static class Extensions
{
    public static async Task<int> ClaimPortAsync(this WebApplicationBuilder builder)
    {
        using var client = new NamedPipeClient();
        string? portNumber = await client.SendCommandToSupervisorAndDisconnectAsync(
            builder.Configuration, $"endpoints claim \"{Environment.MachineName}\" \"{HostingInformation.RunnerName}\"");

        if (string.IsNullOrWhiteSpace(portNumber))
            throw new InvalidOperationException($"Failed to claim an endpoint for runner '{HostingInformation.RunnerName}' on machine '{Environment.MachineName}'.");

        return int.Parse(portNumber, CultureInfo.InvariantCulture);
    }

    public static async Task<(bool IsMaster, string? MasterAddress, string? LocalAddress)> ClaimRoleAsync(this WebApplicationBuilder builder, string role)
    {
        using var client = new NamedPipeClient();
        var masterAddress = await client.SendCommandToSupervisorAsync(
            builder.Configuration, $"roles claim \"{role}\" \"{Environment.MachineName}\" \"{HostingInformation.RunnerName}\"");

        var localAddress = await client.SendCommandToSupervisorAndDisconnectAsync(
            builder.Configuration, $"endpoints query \"{HostingInformation.RunnerName}\"");

        return (string.IsNullOrWhiteSpace(masterAddress), masterAddress, localAddress);
    }

    public static (string Path, string Password) ResolveCertificate(this IHostApplicationBuilder builder)
    {
        var password = Environment.GetEnvironmentVariable(WellKnownNames.WebServerCertificatePass) ?? "";
        var resolvedPath = HostingInformation.GetFullPath(
            Environment.GetEnvironmentVariable(WellKnownNames.WebServerCertificatePath) ?? "");

        if (!File.Exists(resolvedPath))
            throw new FileNotFoundException($"Certificate file not found at: {resolvedPath}");

        return (resolvedPath, password);
    }

    public static Task DelegateConfigureServicesAsync(this IHostApplicationBuilder builder)
    {
        return RegisterServicesActivity
            .FromBuilder(builder)
            .ConfigureBuilder(builder, CancellationToken.None);
    }

    public static async Task DelegateConfigureRoutesAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        foreach (var activity in scope.ServiceProvider.GetServices<IActivity>())
            await activity.ConfigureApplication(app, CancellationToken.None);
    }
}
