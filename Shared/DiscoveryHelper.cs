// ******************************************************************************
// DO NOT CHANGE THIS FILE! It's an helper file shared among multiple projects.
//
// Also, you MUST always add this file to your project as a link. Do not make
// a copy of this, it's used to share some code without creating a library with
// a massive list of dependencies for all the gRPC services we might need in
// multiple places.
// ******************************************************************************

namespace Tinkwell;

public record GrpcService<T>(global::Grpc.Net.Client.GrpcChannel Channel, T Client) 
    : global::System.IDisposable, global::System.IAsyncDisposable
{
    public void Dispose()
        => DisposeAsync().GetAwaiter().GetResult();

    public async global::System.Threading.Tasks.ValueTask DisposeAsync()
    {
        await Channel.ShutdownAsync();
        Channel.Dispose();
    }
}

internal sealed class DiscoveryHelper : IAsyncDisposable, IDisposable
{
    public DiscoveryHelper(global::Microsoft.Extensions.Configuration.IConfiguration configuration, global::Tinkwell.Bootstrapper.Ipc.INamedPipeClient pipeClient)
    {
        _configuration = configuration;
        _pipeClient = pipeClient;
    }

    public async Task<global::Tinkwell.Services.Discovery.DiscoveryClient> GetDiscoveryAsync(global::System.Threading.CancellationToken cancellationToken)
    {
        if (_discovery is not null)
            return _discovery;

        var address = await global::Tinkwell.Bootstrapper.HostingInformation.ResolveDiscoveryServiceAddressAsync(_configuration, _pipeClient);
        _discoveryChannel = global::Grpc.Net.Client.GrpcChannel.ForAddress(address);
        return _discovery = new global::Tinkwell.Services.Discovery.DiscoveryClient(_discoveryChannel);
    }

    public async Task<GrpcService<T>> FindServiceAsync<T>(string fullName, Func<global::Grpc.Net.Client.GrpcChannel, T> factory, global::System.Threading.CancellationToken cancellationToken = default)
    {
        var discovery = await GetDiscoveryAsync(cancellationToken);
        var request = new global::Tinkwell.Services.DiscoveryFindRequest { Name = fullName };
        var addressInfo = await discovery.FindAsync(request, cancellationToken: cancellationToken);
        var channel = global::Grpc.Net.Client.GrpcChannel.ForAddress(addressInfo.Host);

        return new global::Tinkwell.GrpcService<T>(channel, factory(channel));
    }

    public void Dispose()
    {
        DisposeAsync(disposing: true).GetAwaiter().GetResult();
        global::System.GC.SuppressFinalize(this);
    }

    public ValueTask DisposeAsync()
        => DisposeAsync(true);

    private readonly global::Microsoft.Extensions.Configuration.IConfiguration _configuration;
    private readonly global::Tinkwell.Bootstrapper.Ipc.INamedPipeClient _pipeClient;
    private global::Grpc.Net.Client.GrpcChannel? _discoveryChannel;
    private global::Tinkwell.Services.Discovery.DiscoveryClient? _discovery;
    private bool _disposed;

    private async ValueTask DisposeAsync(bool disposing)
    {
        if (_disposed)
            return;

        try
        {
            if (disposing)
            {
                if (_discoveryChannel is not null)
                {
                    await _discoveryChannel.ShutdownAsync();
                    _discoveryChannel.Dispose();
                    _discoveryChannel = null;
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


