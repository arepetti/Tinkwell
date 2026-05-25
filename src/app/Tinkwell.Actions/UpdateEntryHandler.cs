using System.Globalization;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;
using Tinkwell.Configuration;
using Tinkwell.Configuration.Parser;
using Tinkwell.Events;
using Tinkwell.Expressions;
using Tinkwell.Runner;
using Tinkwell.Actions.Abstractions;
using StoreGrpc = Tinkwell.Runlet.Store.Grpc.V1;

namespace Tinkwell.Actions.Store;

/// <summary>
/// External action handler that writes a key-value entry to the state store.
/// </summary>
/// <remarks>
/// Parameters:
/// <list type="bullet">
///   <item><c>bucket</c> (required) — the bucket identifier.</item>
///   <item><c>key</c> (required) — the entry key.</item>
///   <item><c>value</c> (required) — the entry value.</item>
///   <item><c>namespace</c> (optional) — the key namespace.</item>
///   <item><c>ttl</c> (optional) — time-to-live in seconds.</item>
/// </list>
/// </remarks>
public sealed class UpdateEntryHandler : IActionHandler
{
    private readonly IServiceDiscovery _discovery;
    private readonly ILogger<UpdateEntryHandler> _logger;

    public UpdateEntryHandler(IServiceDiscovery discovery, ILogger<UpdateEntryHandler> logger)
    {
        _discovery = discovery;
        _logger = logger;
    }

    public string Name => "update-entry";

    public async Task ExecuteAsync(
        EventEnvelope trigger,
        IReadOnlyDictionary<string, ConfigValue> parameters,
        IExpressionEvaluator evaluator,
        CancellationToken cancellationToken)
    {
        var bucket = await ActionParameterResolver.ResolveRequiredAsync(
            "bucket", parameters, trigger, evaluator, cancellationToken);
        var key = await ActionParameterResolver.ResolveRequiredAsync(
            "key", parameters, trigger, evaluator, cancellationToken);
        var value = await ActionParameterResolver.ResolveRequiredAsync(
            "value", parameters, trigger, evaluator, cancellationToken);
        var ns = await ActionParameterResolver.ResolveOptionalAsync(
            "namespace", parameters, trigger, evaluator, cancellationToken);
        var ttlStr = await ActionParameterResolver.ResolveOptionalAsync(
            "ttl", parameters, trigger, evaluator, cancellationToken);

        var client = await GetClientAsync(cancellationToken);
        if (client is null)
            return;

        var request = new StoreGrpc.SetRequest
        {
            BucketId = bucket,
            Key = key,
            Value = value,
        };

        if (ns is not null)
            request.KeyNamespace = ns;

        if (ttlStr is not null && int.TryParse(ttlStr, CultureInfo.InvariantCulture, out var ttl))
            request.TtlSeconds = ttl;

        await client.SetAsync(request, cancellationToken: cancellationToken);
        _logger.LogDebug("update-entry: {Bucket}/{Key} = {Value}", bucket, key, value);
    }

    private async Task<StoreGrpc.StateStore.StateStoreClient?> GetClientAsync(CancellationToken ct)
    {
        try
        {
            var svc = await _discovery.DiscoverAsync("store", ct);

            if (svc is null)
            {
                _logger.LogWarning("State store service not found");
                return null;
            }

            var channel = GrpcChannel.ForAddress(svc.Url);
            return new StoreGrpc.StateStore.StateStoreClient(channel);
        }
        catch (OutOfMemoryException) { Environment.FailFast("Out of memory"); throw; }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to discover state store service");
            return null;
        }
    }
}