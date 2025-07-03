using Grpc.Net.Client;
using Tinkwell.Bootstrapper.Ipc;
using Tinkwell.Services;

namespace Tinkwell.Cli.Commands;

static class DiscoveryHelpers
{
    public static async Task<GrpcService<Discovery.DiscoveryClient>> FindDiscoveryServiceAsync(CommonSettings settings)
    {
        var channel = GrpcChannel.ForAddress(await ResolveDiscoveryServiceAddressAsync(settings));
        var service = new Discovery.DiscoveryClient(channel);
        return new(channel, service);
    }

    private static async Task<string> ResolveDiscoveryServiceAddressAsync(CommonSettings settings)
    {
        using var client = new NamedPipeClient();
        await client.ConnectAsync(settings.Machine, settings.Pipe, TimeSpan.FromSeconds(settings.Timeout));
        try
        {
            var address = await client.SendCommandAndWaitReplyAsync(
                $"roles query \"{WellKnownNames.DiscoveryServiceRoleName}\"");

            if (address is null || address.StartsWith("Error: "))
                throw new InvalidOperationException("Cannot resolve the address of the Discovery Service.");

            return address;
        }
        finally
        {
            if (client.IsConnected)
                await client.SendCommandAsync("exit");
        }
    }

}
