using System.Collections.Concurrent;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Tinkwell.Measures;
using Tinkwell.Runlet.Store.Grpc.V1;

namespace Tinkwell.Runlet.Measures.Registry;

/// <summary>
/// Store-backed implementation of <see cref="IMeasureRegistry"/>. Persists
/// definitions under the <c>_meta/{name}</c> key and values under <c>{name}</c>,
/// using the state store gRPC service.
/// </summary>
internal sealed class MeasureRegistry : IMeasureRegistry
{
    private const string MetaPrefix = "_meta/";
    private const string MetaNamespace = "_meta";

    private readonly StateStore.StateStoreClient _client;
    private readonly string _bucketId;
    private readonly ILogger<MeasureRegistry> _logger;
    private readonly ConcurrentDictionary<string, (MeasureDefinition Def, MeasureMetadata Meta)> _cache = new();
    private readonly ConcurrentDictionary<string, string> _pendingCorrelations = new();

    public event EventHandler<ValueChangedEventArgs>? ValueChanged;

    public MeasureRegistry(
        StateStore.StateStoreClient client, string bucketId, ILogger<MeasureRegistry> logger)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentException.ThrowIfNullOrWhiteSpace(bucketId);
        ArgumentNullException.ThrowIfNull(logger);

        _client = client;
        _bucketId = bucketId;
        _logger = logger;
    }

    public async Task RegisterAsync(MeasureDefinition definition, MeasureMetadata? metadata = null,
        MeasureValue? initialValue = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ValidateName(definition.Name);

        if (definition.Type == MeasureType.Number && definition.Unit is not null
            && !Quant.IsValidUnit(definition.QuantityType, definition.Unit))
        {
            throw new MeasureException(
                $"Unit '{definition.Unit}' is not valid for quantity type '{definition.QuantityType}'.");
        }

        metadata ??= new MeasureMetadata();
        var json = MeasureJsonSerializer.SerializeDefinition(definition, metadata);

        await _client.SetAsync(new SetRequest
        {
            BucketId = _bucketId,
            KeyNamespace = MetaNamespace,
            Key = definition.Name,
            Value = json,
        }, cancellationToken: ct);

        _cache[definition.Name] = (definition, metadata);

        if (initialValue is { } iv && iv.Type != MeasureValueType.Undefined)
        {
            if (definition.Precision is int precision && iv.Type == MeasureValueType.Number)
                iv = Quant.Round(iv, precision);

            var valueJson = MeasureJsonSerializer.SerializeValue(iv);

            await _client.SetAsync(new SetRequest
            {
                BucketId = _bucketId,
                Key = definition.Name,
                Value = valueJson,
                TtlSeconds = definition.Ttl is TimeSpan ttl ? (int)ttl.TotalSeconds : 0,
            }, cancellationToken: ct);
        }
    }

    public async Task UpdateAsync(string name, MeasureValue value,
        string? correlationId = null, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var (def, _) = await GetCachedDefinitionAsync(name, ct);

        Validate(def, value);

        if (def.Precision is int precision && value.Type == MeasureValueType.Number)
            value = Quant.Round(value, precision);

        correlationId ??= ShortIdGenerator.NewId();

        var json = MeasureJsonSerializer.SerializeValue(value);

        await _client.SetAsync(new SetRequest
        {
            BucketId = _bucketId,
            Key = name,
            Value = json,
            TtlSeconds = def.Ttl is TimeSpan ttl ? (int)ttl.TotalSeconds : 0,
        }, cancellationToken: ct);

        _pendingCorrelations[name] = correlationId;
    }

    public async Task UpdateManyAsync(IEnumerable<(string Name, MeasureValue Value)> measures,
        string? correlationId = null, CancellationToken ct = default)
    {
        correlationId ??= ShortIdGenerator.NewId();

        var batch = new SetManyRequest();
        var names = new List<string>();

        foreach (var (name, value) in measures)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);

            var (def, _) = await GetCachedDefinitionAsync(name, ct);
            Validate(def, value);

            var rounded = def.Precision is int precision && value.Type == MeasureValueType.Number
                ? Quant.Round(value, precision)
                : value;

            names.Add(name);

            batch.Entries.Add(new SetRequest
            {
                BucketId = _bucketId,
                Key = name,
                Value = MeasureJsonSerializer.SerializeValue(rounded),
                TtlSeconds = def.Ttl is TimeSpan ttl ? (int)ttl.TotalSeconds : 0,
            });
        }

        if (batch.Entries.Count > 0)
        {
            await _client.SetManyAsync(batch, cancellationToken: ct);

            foreach (var name in names)
                _pendingCorrelations[name] = correlationId;
        }
    }

    public async Task<Measure?> FindAsync(string name, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var cached = await TryGetCachedDefinitionAsync(name, ct);
        if (cached is null)
            return null;

        var (def, meta) = cached.Value;

        try
        {
            var resp = await _client.GetAsync(new GetRequest
            {
                BucketId = _bucketId,
                Key = name,
            }, cancellationToken: ct);

            var value = MeasureJsonSerializer.DeserializeValue(def, resp.Value);

            return new Measure
            {
                Definition = def,
                Metadata = meta,
                Value = value,
            };
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
        {
            return new Measure
            {
                Definition = def,
                Metadata = meta,
                Value = MeasureValue.Undefined,
            };
        }
    }

    public async Task<IReadOnlyList<Measure>> FindAllAsync(CancellationToken ct = default)
    {
        var measures = new List<Measure>();

        var metaCall = _client.List(new ListRequest
        {
            BucketId = _bucketId,
            KeyNamespace = MetaNamespace,
        }, cancellationToken: ct);

        await foreach (var entry in metaCall.ResponseStream.ReadAllAsync(ct))
        {
            try
            {
                var (def, meta) = MeasureJsonSerializer.DeserializeDefinition(entry.Key, entry.Value);
                _cache[def.Name] = (def, meta);

                MeasureValue value = MeasureValue.Undefined;
                try
                {
                    var resp = await _client.GetAsync(new GetRequest
                    {
                        BucketId = _bucketId,
                        Key = def.Name,
                    }, cancellationToken: ct);

                    value = MeasureJsonSerializer.DeserializeValue(def, resp.Value);
                }
                catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
                {
                }

                measures.Add(new Measure
                {
                    Definition = def,
                    Metadata = meta,
                    Value = value,
                });
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (OutOfMemoryException) { Environment.FailFast("Out of memory"); }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to process measure list entry (key: {Key})",
                    entry.Key);
            }
        }

        return measures;
    }

    public async Task<MeasureDefinition?> FindDefinitionAsync(string name, CancellationToken ct = default)
    {
        var cached = await TryGetCachedDefinitionAsync(name, ct);
        return cached?.Def;
    }

    public async Task WatchAsync(CancellationToken ct = default)
    {
        var valueCache = new ConcurrentDictionary<string, MeasureValue>();

        var call = _client.Watch(new WatchRequest
        {
            BucketId = _bucketId,
        }, cancellationToken: ct);

        await foreach (var evt in call.ResponseStream.ReadAllAsync(ct))
        {
            if (evt.KeyNamespace == MetaNamespace)
            {
                if (evt.EventType == EventType.Set)
                {
                    try
                    {
                        var (def, meta) = MeasureJsonSerializer.DeserializeDefinition(
                            evt.Key, evt.Value);
                        _cache[def.Name] = (def, meta);
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (OutOfMemoryException) { Environment.FailFast("Out of memory"); }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(
                            ex,
                            "Failed to deserialize measure meta in watch (key: {Key})",
                            evt.Key);
                    }
                }
                else if (evt.EventType == EventType.Delete
                    || evt.EventType == EventType.Expired)
                {
                    _cache.TryRemove(evt.Key, out _);
                }

                continue;
            }

            _pendingCorrelations.TryRemove(evt.Key, out var correlationId);

            if (evt.EventType == EventType.Delete
                || evt.EventType == EventType.Expired)
            {
                valueCache.TryRemove(evt.Key, out var old);
                ValueChanged?.Invoke(this, new ValueChangedEventArgs
                {
                    Name = evt.Key,
                    OldValue = old,
                    NewValue = MeasureValue.Undefined,
                    CorrelationId = correlationId,
                });
                continue;
            }

            if (evt.EventType == EventType.Set && _cache.TryGetValue(evt.Key, out var cached))
            {
                try
                {
                    var newValue = MeasureJsonSerializer.DeserializeValue(cached.Def, evt.Value);
                    var oldValue = valueCache.TryGetValue(evt.Key, out var prev)
                        ? prev : MeasureValue.Undefined;

                    valueCache[evt.Key] = newValue;

                    ValueChanged?.Invoke(this, new ValueChangedEventArgs
                    {
                        Name = evt.Key,
                        OldValue = oldValue,
                        NewValue = newValue,
                        CorrelationId = correlationId,
                    });
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (OutOfMemoryException) { Environment.FailFast("Out of memory"); }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        ex,
                        "Failed to deserialize measure value in watch (key: {Key})",
                        evt.Key);
                }
            }
        }
    }

    private static void ValidateName(string name)
    {
        if (name.StartsWith('_'))
            throw new MeasureException(
                $"Measure names starting with '_' are reserved. Got: '{name}'.");
    }

    private static void Validate(MeasureDefinition definition, MeasureValue value)
    {
        if (!definition.IsCompatibleWith(value))
            throw new MeasureException(
                $"Value type '{value.Type}' is incompatible with measure type '{definition.Type}' for '{definition.Name}'.");

        if (definition.Attributes.HasFlag(MeasureAttributes.Constant))
            throw new MeasureException(
                $"Measure '{definition.Name}' is constant and cannot be updated.");

        if (value.Type == MeasureValueType.Number)
        {
            var numericValue = value.AsDouble();

            if (definition.Minimum is double min && numericValue < min)
                throw new MeasureException(
                    $"Value {numericValue} is below minimum {min} for measure '{definition.Name}'.");

            if (definition.Maximum is double max && numericValue > max)
                throw new MeasureException(
                    $"Value {numericValue} is above maximum {max} for measure '{definition.Name}'.");
        }
    }

    private async Task<(MeasureDefinition Def, MeasureMetadata Meta)> GetCachedDefinitionAsync(
        string name, CancellationToken ct)
    {
        return await TryGetCachedDefinitionAsync(name, ct)
            ?? throw new MeasureException($"Measure '{name}' is not registered.");
    }

    private async Task<(MeasureDefinition Def, MeasureMetadata Meta)?> TryGetCachedDefinitionAsync(
        string name, CancellationToken ct)
    {
        if (_cache.TryGetValue(name, out var cached))
            return cached;

        try
        {
            var resp = await _client.GetAsync(new GetRequest
            {
                BucketId = _bucketId,
                KeyNamespace = MetaNamespace,
                Key = name,
            }, cancellationToken: ct);

            var (def, meta) = MeasureJsonSerializer.DeserializeDefinition(name, resp.Value);
            _cache[name] = (def, meta);
            return (def, meta);
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
        {
            return null;
        }
    }
}