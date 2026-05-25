using System.Collections.Concurrent;
using Grpc.Net.Client;
using Tinkwell.Runner;

namespace Tinkwell.Runlet.ProtobufGateway;

/// <summary>
/// Caches <see cref="GrpcChannel"/> instances keyed by proto service name.
/// On a cache miss, discovers the target service via <see cref="IServiceDiscovery"/>
/// and creates a channel to it.
/// </summary>
internal sealed class ServiceCache : IAsyncDisposable
{
    private readonly IServiceDiscovery _discovery;
    private readonly ConcurrentDictionary<string, GrpcChannel> _channels = new();

    public ServiceCache(IServiceDiscovery discovery)
    {
        _discovery = discovery;
    }

    /// <summary>
    /// Returns a cached <see cref="GrpcChannel"/> for the given service,
    /// or discovers and creates one. Returns <see langword="null"/> if
    /// the service cannot be found.
    /// </summary>
    public async ValueTask<GrpcChannel?> GetChannelAsync(
        string serviceName, CancellationToken ct)
    {
        if (_channels.TryGetValue(serviceName, out var existing))
            return existing;

        var definition = await _discovery.DiscoverByNameAsync(serviceName, ct);
        if (definition is null)
            return null;

        var channel = GrpcChannel.ForAddress(definition.Url);
        var cached = _channels.GetOrAdd(serviceName, channel);

        if (!ReferenceEquals(cached, channel))
            channel.Dispose();

        return cached;
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var channel in _channels.Values)
        {
            await channel.ShutdownAsync();
            channel.Dispose();
        }

        _channels.Clear();
    }
}
