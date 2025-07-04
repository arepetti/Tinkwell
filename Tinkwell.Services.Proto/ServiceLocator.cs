using Grpc.Net.Client;
using Microsoft.Extensions.Configuration;
using Tinkwell.Bootstrapper;
using Tinkwell.Bootstrapper.Ipc;

namespace Tinkwell;

public sealed class ServiceLocator : IAsyncDisposable, IDisposable
{
    public ServiceLocator(IConfiguration configuration, INamedPipeClient pipeClient)
    {
        _configuration = configuration;
        _pipeClient = pipeClient;
    }

    public async Task<Services.Discovery.DiscoveryClient> FindDiscoveryAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_discovery is not null)
            return _discovery.Client;

        var address = await HostingInformation.ResolveDiscoveryServiceAddressAsync(_configuration, _pipeClient);
        if (string.IsNullOrWhiteSpace(address))
        {
            throw new InvalidOperationException($"Cannot resolve the address for the Discovery Service host. " +
                                                $"Please ensure that the Supervisor is running and accessible.");
        }

        var channel = GrpcChannel.ForAddress(address);
        _discovery = new GrpcService<Services.Discovery.DiscoveryClient>(channel, new Services.Discovery.DiscoveryClient(channel));

        return _discovery.Client;
    }

    public async Task<GrpcService<T>> FindServiceAsync<T>(string name, Func<GrpcChannel, T> factory, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var discovery = await FindDiscoveryAsync(cancellationToken);
        var request = new Services.DiscoveryFindRequest { Name = name };
        var addressInfo = await discovery.FindAsync(request, cancellationToken: cancellationToken);
        var channel = GrpcChannel.ForAddress(addressInfo.Host);

        return new GrpcService<T>(channel, factory(channel));
    }

    public async Task<GrpcService<Services.Store.StoreClient>> FindStoreAsync(CancellationToken cancellationToken = default)
    {
        // Note that we are intentionally using Descriptor.Name instead of Descriptor.FullName. This is because it's possible,
        // in theory and with some configuration, to have multiple stores in the system. Using Descriptor.Name allows us to
        // search for the service using its Family Name (which is the same as the service name) instead of the full
        // name (which must be unique). It's not supported out-of-the box but, at least, those using this library
        // won't need to be updated if we decide to add multiple stores in the future.
        return await FindServiceAsync(Services.Store.Descriptor.Name, c => new Services.Store.StoreClient(c), cancellationToken);
    }

    public async Task<GrpcService<Services.EventsGateway.EventsGatewayClient>> FindEventsGatewayAsync(CancellationToken cancellationToken = default)
        => await FindServiceAsync(Services.EventsGateway.Descriptor.FullName, c => new Services.EventsGateway.EventsGatewayClient(c), cancellationToken);

    public void Dispose()
    {
        DisposeAsync(disposing: true).GetAwaiter().GetResult();
        GC.SuppressFinalize(this);
    }

    public ValueTask DisposeAsync()
        => DisposeAsync(true);

    private readonly IConfiguration _configuration;
    private readonly INamedPipeClient _pipeClient;
    private GrpcService<Services.Discovery.DiscoveryClient>? _discovery;
    private bool _disposed;

    private async ValueTask DisposeAsync(bool disposing)
    {
        if (_disposed)
            return;

        try
        {
            if (disposing)
            {
                if (_discovery is not null)
                {
                    await _discovery.Channel.ShutdownAsync();
                    _discovery.Channel.Dispose();
                    _discovery = null;
                }
            }
        }
        finally
        {
            _disposed = true;
        }
    }
}


