using System.Text.Json;
using System.Text.Json.Serialization;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;
using Tinkwell.Health;
using Tinkwell.Runner;
using Tinkwell.Runlet.Store.Grpc.V1;

namespace Tinkwell.Runner.Hosting;

/// <summary>
/// Writes <see cref="HealthReport"/> JSON to the state store's
/// <c>_health</c> bucket. Lazily discovers the store on first use.
/// </summary>
internal sealed class StoreHealthReportWriter : IHealthReportWriter, IAsyncDisposable
{
    private const string HealthBucketId = "_health";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly IServiceDiscovery _discovery;
    private readonly HealthMonitorOptions _options;
    private readonly ILogger _logger;

    private StateStore.StateStoreClient? _client;
    private bool _bucketConfigured;

    public StoreHealthReportWriter(
        IServiceDiscovery discovery,
        HealthMonitorOptions options,
        ILogger<StoreHealthReportWriter> logger)
    {
        _discovery = discovery;
        _options = options;
        _logger = logger;
    }

    public async Task WriteAsync(string runnerName, HealthReport report, CancellationToken ct)
    {
        var client = await GetClientAsync(ct);
        if (client is null)
            return;

        if (!_bucketConfigured)
        {
            try
            {
                await client.ConfigureBucketAsync(new ConfigureBucketRequest
                {
                    BucketId = HealthBucketId,
                    Discoverable = false,
                }, cancellationToken: ct);
                _bucketConfigured = true;
            }
            catch (OutOfMemoryException) { Environment.FailFast("Out of memory"); }
            catch (Exception ex)
            {
                _logger.LogTrace(ex, "Failed to configure health bucket");
            }
        }

        var json = JsonSerializer.Serialize(report, JsonOptions);

        await client.SetAsync(new SetRequest
        {
            BucketId = HealthBucketId,
            Key = runnerName,
            Value = json,
            TtlSeconds = (int)_options.Ttl.TotalSeconds,
        }, cancellationToken: ct);
    }

    private async Task<StateStore.StateStoreClient?> GetClientAsync(CancellationToken ct)
    {
        if (_client is not null)
            return _client;

        try
        {
            var service = await _discovery.DiscoverAsync("store", ct);
            if (service is null)
            {
                _logger.LogTrace("State store not yet available for health reporting");
                return null;
            }

            _client = await _discovery.CreateInstanceAsync<StateStore.StateStoreClient>(service, ct);
            return _client;
        }
        catch (OutOfMemoryException) { Environment.FailFast("Out of memory"); throw; }
        catch (Exception ex)
        {
            _logger.LogTrace(ex, "Store discovery failed, will retry next tick");
            return null;
        }
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}