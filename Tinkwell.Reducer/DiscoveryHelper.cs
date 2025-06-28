using Grpc.Net.Client;

namespace Tinkwell.Reducer;

public sealed class DiscoveryHelper
{
    private async Task<(Discovery.DiscoveryClient discovery, Store.StoreClient store)> CreateClientsAsync(CancellationToken cancellationToken)
    {
        var discovery = new Discovery.DiscoveryClient(_discoveryChannel);

        if (_storeChannel is null)
        {
            var storeHost = await discovery.FindAsync(new DiscoveryFindRequest { Name = Store.Descriptor.FullName }, cancellationToken: cancellationToken);
            _storeChannel = GrpcChannel.ForAddress(storeHost.Host);
        }

        var store = new Store.StoreClient(_storeChannel);

        return (discovery, store);
    }
}


