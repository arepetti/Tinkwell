using System.Collections.Concurrent;
using System.Net.Security;
using Grpc.Net.Client;
using Tinkwell.Telemetry;

namespace Tinkwell.Runner.Hosting;

/// <summary>
/// Implements <see cref="IServiceDiscovery"/> by delegating discovery
/// queries to the coordinator via <see cref="CoordinatorPipeClient"/> and
/// creating gRPC clients backed by cached <see cref="GrpcChannel"/> instances.
/// When TLS is enabled, channels are created with the appropriate scheme
/// and validation behavior.
/// </summary>
internal sealed class ServiceDiscovery : IServiceDiscovery, IDisposable
{
    private readonly CoordinatorPipeClient _client;
    private readonly TlsOptions _tls;
    private readonly ConcurrentDictionary<string, GrpcChannel> _channels = new();

    public ServiceDiscovery(CoordinatorPipeClient client, TlsOptions tlsOptions)
    {
        _client = client;
        _tls = tlsOptions;
    }

    public async Task<ServiceDefinition?> DiscoverByNameAsync(
        string name, CancellationToken cancellationToken = default)
    {
        using var span = OtTraces.Source.Timed(
            OtTraces.Discovery, OtMetrics.DiscoveryDuration,
            (OtTraces.ServiceName, name));

        try
        {
            var result = await _client.FindServiceAsync(name, cancellationToken);
            var status = result is not null ? "found" : "not_found";
            RecordDiscovery(name, status);
            span.SetTag(OtTraces.DiscoveryResult, status);
            return result;
        }
        catch
        {
            RecordDiscovery(name, "error");
            span.Error("discovery failed");
            throw;
        }
    }

    public async Task<IReadOnlyList<ServiceDefinition>> SearchByNamePartialMatchAsync(
        string? query = null, CancellationToken cancellationToken = default)
    {
        using var span = OtTraces.Source.Timed(
            OtTraces.Discovery, OtMetrics.DiscoveryDuration,
            (OtTraces.ServiceName, query ?? "*"));

        try
        {
            var result = await _client.ListServicesAsync(query, cancellationToken);
            RecordDiscovery(query ?? "*", "found");
            span.SetTag(OtTraces.DiscoveryResult, "found");
            return result;
        }
        catch
        {
            RecordDiscovery(query ?? "*", "error");
            span.Error("discovery failed");
            throw;
        }
    }

    private static void RecordDiscovery(string serviceName, string result) =>
        OtMetrics.DiscoveryCalls.Add(1,
            new KeyValuePair<string, object?>(OtTraces.ServiceName, serviceName),
            new KeyValuePair<string, object?>(OtTraces.DiscoveryResult, result));

    public Task<T> CreateInstanceAsync<T>(
        ServiceDefinition service, CancellationToken cancellationToken = default)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(service);

        return service.Type switch
        {
            ServiceType.Grpc => Task.FromResult(CreateGrpcClient<T>(service)),
            _ => throw new NotSupportedException(
                $"Client creation for service type '{service.Type}' is not supported.")
        };
    }

    public void Dispose()
    {
        foreach (var channel in _channels.Values)
            channel.Dispose();

        _channels.Clear();
    }

    private T CreateGrpcClient<T>(ServiceDefinition service) where T : class
    {
        using var span = OtTraces.Source.Timed(
            OtTraces.ChannelCreate, OtMetrics.ChannelCreateDuration,
            (OtTraces.ChannelHost, service.Host));

        bool cached = _channels.TryGetValue(service.Host, out _);
        var channel = cached
            ? _channels[service.Host]
            : _channels.GetOrAdd(service.Host, CreateChannel);

        if (cached)
            OtMetrics.ChannelCacheHits.Inc(OtTraces.ChannelHost, service.Host);
        else
            OtMetrics.ChannelCacheMisses.Inc(OtTraces.ChannelHost, service.Host);

        span.SetTag(OtTraces.ChannelCached, cached);
        return (T)Activator.CreateInstance(typeof(T), channel)!;
    }

    private GrpcChannel CreateChannel(string host)
    {
        var address = $"{_tls.Scheme}://{host}";

        if (_tls.Mode != TlsMode.SelfSigned)
            return GrpcChannel.ForAddress(address);

        var handler = new SocketsHttpHandler
        {
            SslOptions = new SslClientAuthenticationOptions
            {
                RemoteCertificateValidationCallback = (_, _, _, _) => true
            }
        };

        return GrpcChannel.ForAddress(address, new GrpcChannelOptions
        {
            HttpHandler = handler
        });
    }
}
