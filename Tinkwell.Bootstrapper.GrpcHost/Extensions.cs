using System.Globalization;
using Tinkwell.Bootstrapper.Ipc;
using Tinkwell.Bootstrapper.Ipc.Extensions;

namespace Tinkwell.Bootstrapper.GrpcHost;

static class Extensions
{
    public static string RunnerName
        => Environment.GetEnvironmentVariable(WellKnownNames.RunnerNameEnvironmentVariable) ?? "";

    public static int ClaimPort(this WebApplicationBuilder builder)
    {
        var client = new NamedPipeClient();
        string? portNumber = client.SendCommandToSupervisorAndDisconnectAsync(
            builder.Configuration, $"endpoints claim \"{Environment.MachineName}\" \"{RunnerName}\"")
            .GetAwaiter().GetResult();

        if (string.IsNullOrWhiteSpace(portNumber))
            throw new InvalidOperationException($"Failed to claim an endpoint for runner '{RunnerName}' on machine '{Environment.MachineName}'.");

        return int.Parse(portNumber, CultureInfo.InvariantCulture);
    }

    public static bool TryClaimRole(this WebApplicationBuilder builder, string role, out string? masterAddress, out string? localAddress)
    {
        var client = new NamedPipeClient();
        masterAddress = client.SendCommandToSupervisorAndDisconnectAsync(
            builder.Configuration, $"roles claim \"{role}\" \"{Environment.MachineName}\" \"{RunnerName}\"")
            .GetAwaiter().GetResult();

        localAddress = client.SendCommandToSupervisorAndDisconnectAsync(
            builder.Configuration, $"endpoints query \"{RunnerName}\"")
            .GetAwaiter().GetResult();

        return string.IsNullOrWhiteSpace(masterAddress);
    }

    public static (string Path, string Password) ResolveCertificate(this IHostApplicationBuilder builder)
    {
        var path = Path.Combine(builder.Environment.ContentRootPath,
            Environment.GetEnvironmentVariable("TINKWELL_CERT_PATH") ?? "");
        var password = Environment.GetEnvironmentVariable("TINKWELL_CERT_PASS") ?? "";

        return (path, password);
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
