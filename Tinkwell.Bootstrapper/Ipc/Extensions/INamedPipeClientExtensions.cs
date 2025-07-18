using Microsoft.Extensions.Configuration;

namespace Tinkwell.Bootstrapper.Ipc.Extensions;

/// <summary>
/// Extension methods for <c>INamedPipeClient</c>.
/// </summary>
public static class INamedPipeClientExtensions
{
    public static string? SendCommandToSupervisorAndDisconnect(this INamedPipeClient client, IConfiguration configuration, string command)
    {
        client.Connect(configuration);
        try
        {
            return client.SendCommandAndWaitReply(command);
        }
        finally
        {
            client.SendCommand("exit");
            client.Disconnect();
        }
    }

    public static Task<string?> SendCommandToSupervisorAsync(this INamedPipeClient client, IConfiguration configuration, string command, CancellationToken cancellationToken = default)
    {
        client.Connect(configuration);
        return client.SendCommandAndWaitReplyAsync(command);
    }

    public static async Task<string?> SendCommandToSupervisorAndDisconnectAsync(this INamedPipeClient client, IConfiguration configuration, string command, CancellationToken cancellationToken = default)
    {
        client.Connect(configuration);
        try
        {
            return await client.SendCommandAndWaitReplyAsync(command, cancellationToken);
        }
        finally
        {
            await client.SendCommandAsync("exit", cancellationToken);
            client.Disconnect();
        }
    }

    public static async Task<T> SendCommandToSupervisorAndDisconnectAsync<T>(this INamedPipeClient client, IConfiguration configuration, string command, CancellationToken cancellationToken = default)
    {
        client.Connect(configuration);
        try
        {
            return await client.SendCommandAndWaitReplyAsync<T>(command, cancellationToken);
        }
        finally
        {
            await client.SendCommandAsync("exit", cancellationToken);
            client.Disconnect();
        }
    }

    private static void Connect(this INamedPipeClient client, IConfiguration configuration)
    {
        string serverName = configuration.GetValue("Supervisor:CommandServer:ServerName", ".");
        string pipeName = configuration.GetValue("Supervisor:CommandServer:PipeName",
            WellKnownNames.SupervisorCommandServerPipeName);
        client.Connect(serverName, pipeName);
    }
}