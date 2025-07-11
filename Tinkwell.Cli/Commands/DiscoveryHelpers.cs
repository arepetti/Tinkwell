using Grpc.Net.Client;
using Tinkwell.Bootstrapper.Ipc;
using Tinkwell.Services;

namespace Tinkwell.Cli.Commands;

static class DiscoveryHelpers
{
    public static async Task<GrpcService<Discovery.DiscoveryClient>> FindDiscoveryServiceAsync(LiveInstanceCommonSettings settings)
    {
        var channel = GrpcChannel.ForAddress(await ResolveDiscoveryServiceAddressAsync(settings));
        var service = new Discovery.DiscoveryClient(channel);
        return new(channel, service);
    }

    public static async Task<GrpcService<Store.StoreClient>> FindStoreServiceAsync(LiveInstanceCommonSettings settings)
    {
        var channel = GrpcChannel.ForAddress(await FindServiceAddressAsync(settings, Store.Descriptor.FullName));
        var service = new Store.StoreClient(channel);
        return new(channel, service);
    }

    public static async Task<GrpcService<EventsGateway.EventsGatewayClient>> FindEventsGatewayServiceAsync(LiveInstanceCommonSettings settings)
    {
        var channel = GrpcChannel.ForAddress(await FindServiceAddressAsync(settings, EventsGateway.Descriptor.FullName));
        var service = new EventsGateway.EventsGatewayClient(channel);
        return new(channel, service);
    }

    public static async Task<string> ResolveDiscoveryServiceAddressAsync(LiveInstanceCommonSettings settings)
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

    private static async Task<string> FindServiceAddressAsync(LiveInstanceCommonSettings settings, string name)
    {
        await using var discovery = await FindDiscoveryServiceAsync(settings);
        var response = await discovery.Client.FindAsync(new() { Name = name });
        return response.Host;
    }
}
