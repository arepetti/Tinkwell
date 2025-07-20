using Grpc.Net.Client;
using Microsoft.Extensions.Configuration;
using System.Security.Cryptography.X509Certificates;
using Tinkwell.Bootstrapper.Hosting;
using Tinkwell.Bootstrapper.Ipc;

namespace Tinkwell;

/// <summary>
/// Helper class that encapsulate searching for gRPC services.
/// </summary>
public sealed class ServiceLocator : IAsyncDisposable, IDisposable
{
    /// <summary>
    /// Creates a new instance of the <see cref="ServiceLocator"/> class.
    /// </summary>
    /// <param name="configuration">Configuration.</param>
    /// <param name="pipeClient">Client to use for communication using named pipes.</param>
    public ServiceLocator(IConfiguration configuration, INamedPipeClient pipeClient)
    {
        _configuration = configuration;
        _pipeClient = pipeClient;
    }

    /// <summary>
    /// Obtains the <see cref="Services.Discovery.DiscoveryClient"/>.
    /// </summary>
    /// <param name="cancellationToken">A token that can be used to request cancellation of the operation.</param>
    /// <returns>An instance of the <c>DiscoveryClient</c> service.</returns>
    /// <exception cref="InvalidOperationException">If the address of the service cannot be resolved.</exception>
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

        var channel = CreateChannel(address);
        _discovery = new GrpcService<Services.Discovery.DiscoveryClient>(channel, new Services.Discovery.DiscoveryClient(channel));

        return _discovery.Client;
    }

    /// <summary>
    /// Fnd a gRPC service by its name.
    /// </summary>
    /// <typeparam name="T">Type of the service to locate.</typeparam>
    /// <param name="name">Full name (or family name or alias) of the service to locate.</param>
    /// <param name="factory">Factory function that creates an instance of the service, give the channel.</param>
    /// <param name="cancellationToken">A token that can be used to request cancellation of the operation.</param>
    /// <returns>A container for the required service and its channel. It must be disposed when the service is not used anymore.</returns>
    public async Task<GrpcService<T>> FindServiceAsync<T>(string name, Func<GrpcChannel, T> factory, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var discovery = await FindDiscoveryAsync(cancellationToken);
        var request = new Services.DiscoveryFindRequest { Name = name };
        var addressInfo = await discovery.FindAsync(request, cancellationToken: cancellationToken);
        var channel = CreateChannel(addressInfo.Host);

        return new GrpcService<T>(channel, factory(channel));
    }

    /// <summary>
    /// Finds the <see cref="Services.Store.StoreClient"/> service.
    /// </summary>
    /// <param name="cancellationToken">A token that can be used to request cancellation of the operation.</param>
    /// <returns>A container for the required service and its channel. It must be disposed when the service is not used anymore.</returns>
    public async Task<GrpcService<Services.Store.StoreClient>> FindStoreAsync(CancellationToken cancellationToken = default)
    {
        // Note that we are intentionally using Descriptor.Name instead of Descriptor.FullName. This is because it's possible,
        // in theory and with some configuration, to have multiple stores in the system. Using Descriptor.Name allows us to
        // search for the service using its Family Name (which is the same as the service name) instead of the full
        // name (which must be unique). It's not supported out-of-the box but, at least, those using this library
        // won't need to be updated if we decide to add multiple stores in the future.
        return await FindServiceAsync(Services.Store.Descriptor.Name, c => new Services.Store.StoreClient(c), cancellationToken);
    }

    /// <summary>
    /// Finds the <see cref="Services.EventsGateway.EventsGatewayClient"/> service.
    /// </summary>
    /// <param name="cancellationToken">A token that can be used to request cancellation of the operation.</param>
    /// <returns>A container for the required service and its channel. It must be disposed when the service is not used anymore.</returns>
    public async Task<GrpcService<Services.EventsGateway.EventsGatewayClient>> FindEventsGatewayAsync(CancellationToken cancellationToken = default)
        => await FindServiceAsync(Services.EventsGateway.Descriptor.FullName, c => new Services.EventsGateway.EventsGatewayClient(c), cancellationToken);

    /// <inheritdoc />
    public void Dispose()
    {
        DisposeAsync(disposing: true).GetAwaiter().GetResult();
        GC.SuppressFinalize(this);
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
        => DisposeAsync(true);

    private readonly IConfiguration _configuration;
    private readonly INamedPipeClient _pipeClient;
    private GrpcService<Services.Discovery.DiscoveryClient>? _discovery;
    private bool _disposed;
    private static X509Certificate2? _clientCertificate;

    private GrpcChannel CreateChannel(string address)
    {
        if (_clientCertificate is null)
        {
            var clientCertificatePath = Environment.GetEnvironmentVariable(WellKnownNames.ClientCertificatePath);
            if (!string.IsNullOrWhiteSpace(clientCertificatePath))
                _clientCertificate = X509CertificateLoader.LoadCertificateFromFile(clientCertificatePath);
        }

        if (_clientCertificate is null)
            return GrpcChannel.ForAddress(address);

        var handler = new HttpClientHandler();
        handler.ClientCertificates.Add(_clientCertificate);
        handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true;

        return GrpcChannel.ForAddress(address, new GrpcChannelOptions { HttpHandler = handler });

    }

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


