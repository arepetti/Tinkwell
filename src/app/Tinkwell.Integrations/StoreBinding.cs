using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Tinkwell.Coap;
using Tinkwell.Expressions;
using Tinkwell.Integration;
using Tinkwell.Runner;
using StoreGrpc = Tinkwell.Runlet.Store.Grpc.V1;

namespace Tinkwell.Integration.Store;

/// <summary>
/// Integration binding for reading, writing, and deleting state store entries
/// through the gRPC state store. CoAP: GET (text or JSON with timestamps if
/// <see cref="CoapContentFormat.ApplicationJson"/> is accepted), POST/PUT (set, value from
/// <c>value</c> or payload), DELETE (removes entry). MQTT: set/upsert only; value from
/// <c>value</c> or the message payload.
/// </summary>
/// <remarks>
/// Resolves the store service via <see cref="IServiceDiscovery"/> (family name <c>store</c>).
/// When the client cannot be obtained, CoAP handlers throw; MQTT handlers log and return
/// <see langword="null"/>.
/// </remarks>
public sealed class StoreBinding : ICoapIntegrationBinding, IMqttIntegrationBinding
{
    private readonly IServiceDiscovery _discovery;
    private readonly ILogger<StoreBinding>? _logger;
    private readonly SemaphoreSlim _clientLock = new(1, 1);
    private StoreGrpc.StateStore.StateStoreClient? _client;

    public StoreBinding(IServiceDiscovery discovery, ILogger<StoreBinding>? logger = null)
    {
        _discovery = discovery;
        _logger = logger;
    }

    public string Name => "store";

    public Task<BindingResult?> HandleAsync(
        IntegrationContext context,
        BindingParameterSet parameters,
        IExpressionEvaluator evaluator,
        CancellationToken ct) =>
        HandleCoapAsync(context, parameters, evaluator, [], ct);

    public async Task<BindingResult?> HandleMqttAsync(
        IntegrationContext context,
        BindingParameterSet parameters,
        IExpressionEvaluator evaluator,
        CancellationToken ct)
    {
        var p = context.ToExpressionParameters();
        var bucket = await BindingParameterResolver.ResolveRequiredAsync("bucket", "Store", parameters, evaluator, p, ct);
        var key = await BindingParameterResolver.ResolveRequiredAsync("key", "Store", parameters, evaluator, p, ct);
        var ns = await BindingParameterResolver.ResolveOptionalAsync("namespace", parameters, evaluator, p, ct) ?? "";
        var client = await GetClientAsync(ct);
        if (client is null)
        {
            _logger?.LogWarning("Store service not found — skipping");
            return null;
        }
        await HandleSetAsync(client, bucket, ns, key, parameters, evaluator, p, context.Payload, ct);
        return null;
    }

    public async Task<BindingResult?> HandleCoapAsync(
        IntegrationContext context,
        BindingParameterSet parameters,
        IExpressionEvaluator evaluator,
        IReadOnlyList<CoapContentFormat> acceptFormats,
        CancellationToken ct)
    {
        var p = context.ToExpressionParameters();
        var bucket = await BindingParameterResolver.ResolveRequiredAsync("bucket", "Store", parameters, evaluator, p, ct);
        var key = await BindingParameterResolver.ResolveRequiredAsync("key", "Store", parameters, evaluator, p, ct);
        var ns = await BindingParameterResolver.ResolveOptionalAsync("namespace", parameters, evaluator, p, ct) ?? "";
        var client = await GetClientAsync(ct);
        if (client is null)
        {
            _logger?.LogWarning("Store service not found");
            throw new InvalidOperationException("Store service not found");
        }

        return context.Method.ToUpperInvariant() switch
        {
            "GET" => await HandleGetAsync(client, bucket, ns, key, acceptFormats, ct),
            "POST" or "PUT" => await HandleSetAsync(client, bucket, ns, key, parameters, evaluator, p, context.Payload, ct),
            "DELETE" => await HandleDeleteAsync(client, bucket, ns, key, ct),
            _ => null,
        };
    }

    private async Task<StoreGrpc.StateStore.StateStoreClient?> GetClientAsync(CancellationToken ct)
    {
        if (_client is not null)
        {
            return _client;
        }

        await _clientLock.WaitAsync(ct);
        try
        {
            if (_client is not null)
            {
                return _client;
            }

            try
            {
                var svc = await _discovery.DiscoverAsync("store", ct);

                if (svc is null)
                {
                    return null;
                }

                _client = await _discovery.CreateInstanceAsync<StoreGrpc.StateStore.StateStoreClient>(svc, ct);
                return _client;
            }
            catch (OutOfMemoryException) { Environment.FailFast("Out of memory"); throw; }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to discover store service");
                return null;
            }
        }
        finally
        {
            _clientLock.Release();
        }
    }

    private static async Task<BindingResult?> HandleGetAsync(
        StoreGrpc.StateStore.StateStoreClient client,
        string bucket, string ns, string key,
        IReadOnlyList<CoapContentFormat> acceptFormats, CancellationToken ct)
    {
        var response = await client.GetAsync(
            new StoreGrpc.GetRequest { BucketId = bucket, KeyNamespace = ns, Key = key },
            cancellationToken: ct);

        if (string.IsNullOrEmpty(response.Value) && response.CreatedAt is null)
        {
            throw new ArgumentException($"Entry '{bucket}/{ns}/{key}' not found");
        }

        if (acceptFormats.Contains(CoapContentFormat.ApplicationJson))
        {
            var json = JsonSerializer.Serialize(new
            {
                value = response.Value,
                created_at = response.CreatedAt?.ToDateTimeOffset(),
                updated_at = response.UpdatedAt?.ToDateTimeOffset(),
            });
            return new BindingResult(Encoding.UTF8.GetBytes(json), CoapContentFormat.ApplicationJson);
        }

        return new BindingResult(
            Encoding.UTF8.GetBytes(response.Value),
            CoapContentFormat.TextPlain);
    }

    private static async Task<BindingResult?> HandleSetAsync(
        StoreGrpc.StateStore.StateStoreClient client,
        string bucket, string ns, string key,
        BindingParameterSet parameters, IExpressionEvaluator evaluator,
        IReadOnlyDictionary<string, object?> exprParams,
        string? payload, CancellationToken ct)
    {
        var value = await BindingParameterResolver.ResolveOptionalAsync("value", parameters, evaluator, exprParams, ct)
            ?? payload ?? "";

        var ttl = await ResolveTtlSecondsAsync(parameters, evaluator, exprParams, ct);

        await client.SetAsync(
            new StoreGrpc.SetRequest
            {
                BucketId = bucket,
                KeyNamespace = ns,
                Key = key,
                Value = value,
                TtlSeconds = ttl,
            },
            cancellationToken: ct);

        return null;
    }

    internal static async Task<int> ResolveTtlSecondsAsync(
        BindingParameterSet parameters,
        IExpressionEvaluator evaluator,
        IReadOnlyDictionary<string, object?> exprParams,
        CancellationToken ct)
    {
        var ttl = 0;
        if (parameters.Properties.TryGetValue("ttl", out var ttlValue))
        {
            var ttlStr = await BindingParameterResolver.ResolveConfigValueAsync(ttlValue, evaluator, exprParams, ct);
            if (ttlStr is not null && int.TryParse(ttlStr, out var parsed))
            {
                ttl = parsed;
            }
        }

        return ttl;
    }

    private static async Task<BindingResult?> HandleDeleteAsync(
        StoreGrpc.StateStore.StateStoreClient client,
        string bucket, string ns, string key, CancellationToken ct)
    {
        var response = await client.DeleteAsync(
            new StoreGrpc.DeleteRequest { BucketId = bucket, KeyNamespace = ns, Key = key },
            cancellationToken: ct);

        if (!response.Found)
        {
            throw new ArgumentException($"Entry '{bucket}/{ns}/{key}' not found");
        }

        return null;
    }
}