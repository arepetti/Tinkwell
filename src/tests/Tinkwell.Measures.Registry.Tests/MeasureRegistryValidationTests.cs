using System.Collections.Concurrent;
using System.Reflection;
using Grpc.Core;
using Microsoft.Extensions.Logging.Abstractions;
using Tinkwell.Measures;
using Tinkwell.Runlet.Measures.Registry;
using Tinkwell.Runlet.Store.Grpc.V1;
using StateStore = Tinkwell.Runlet.Store.Grpc.V1.StateStore;

namespace Tinkwell.Measures.Registry.Tests;

public class MeasureRegistryValidationTests
{
    private const string Bucket = "test-bucket";

    #region In-memory state store (for full registry flow tests)

    private sealed class InMemoryStateStoreClient : StateStore.StateStoreClient
    {
        private readonly ConcurrentDictionary<string, string> _data = new();
        public TaskCompletionSource? GateValueWrite { get; set; }
        public TaskCompletionSource? EnteredValueWrite { get; set; }

        private static string DataKey(string bucketId, string keyNamespace, string key) =>
            $"{bucketId}\0{keyNamespace}\0{key}";

        public override AsyncUnaryCall<GetResponse> GetAsync(GetRequest request, CallOptions options)
        {
            var key = DataKey(request.BucketId, request.KeyNamespace, request.Key);
            if (!_data.TryGetValue(key, out var value))
            {
                return new AsyncUnaryCall<GetResponse>(
                    Task.FromException<GetResponse>(new RpcException(new Status(StatusCode.NotFound, ""))),
                    Task.FromResult(Metadata.Empty),
                    static () => Status.DefaultSuccess,
                    static () => Metadata.Empty,
                    static () => { });
            }

            return new AsyncUnaryCall<GetResponse>(
                Task.FromResult(new GetResponse { Value = value }),
                Task.FromResult(Metadata.Empty),
                static () => Status.DefaultSuccess,
                static () => Metadata.Empty,
                static () => { });
        }

        public override AsyncUnaryCall<SetResponse> SetAsync(SetRequest request, CallOptions options)
        {
            if (string.IsNullOrEmpty(request.KeyNamespace) && GateValueWrite is not null)
            {
                return new AsyncUnaryCall<SetResponse>(
                    WithGateAsync(request, options.CancellationToken),
                    Task.FromResult(Metadata.Empty),
                    static () => Status.DefaultSuccess,
                    static () => Metadata.Empty,
                    static () => { });
            }

            _data[DataKey(request.BucketId, request.KeyNamespace, request.Key)] = request.Value;
            return new AsyncUnaryCall<SetResponse>(
                Task.FromResult(new SetResponse()),
                Task.FromResult(Metadata.Empty),
                static () => Status.DefaultSuccess,
                static () => Metadata.Empty,
                static () => { });
        }

        public override AsyncUnaryCall<SetManyResponse> SetManyAsync(SetManyRequest request, CallOptions options)
        {
            if (GateValueWrite is not null)
            {
                return new AsyncUnaryCall<SetManyResponse>(
                    SetManyWithGateAsync(request, options.CancellationToken),
                    Task.FromResult(Metadata.Empty),
                    static () => Status.DefaultSuccess,
                    static () => Metadata.Empty,
                    static () => { });
            }

            foreach (var entry in request.Entries)
            {
                _data[DataKey(entry.BucketId, entry.KeyNamespace, entry.Key)] = entry.Value;
            }

            return new AsyncUnaryCall<SetManyResponse>(
                Task.FromResult(new SetManyResponse()),
                Task.FromResult(Metadata.Empty),
                static () => Status.DefaultSuccess,
                static () => Metadata.Empty,
                static () => { });
        }

        private async Task<SetResponse> WithGateAsync(SetRequest request, CancellationToken ct)
        {
            EnteredValueWrite?.TrySetResult();
            if (GateValueWrite is not null)
            {
                await GateValueWrite.Task.WaitAsync(ct);
            }

            _data[DataKey(request.BucketId, request.KeyNamespace, request.Key)] = request.Value;
            return new SetResponse();
        }

        private async Task<SetManyResponse> SetManyWithGateAsync(SetManyRequest request, CancellationToken ct)
        {
            EnteredValueWrite?.TrySetResult();
            if (GateValueWrite is not null)
            {
                await GateValueWrite.Task.WaitAsync(ct);
            }

            foreach (var entry in request.Entries)
            {
                _data[DataKey(entry.BucketId, entry.KeyNamespace, entry.Key)] = entry.Value;
            }

            return new SetManyResponse();
        }

        public string? GetStoredValueJson(string name) =>
            _data.GetValueOrDefault(DataKey(Bucket, "", name));
    }

    private static ConcurrentDictionary<string, string> GetPendingCorrelations(MeasureRegistry registry)
    {
        var field = typeof(MeasureRegistry).GetField(
            "_pendingCorrelations", BindingFlags.Instance | BindingFlags.NonPublic);
        return (ConcurrentDictionary<string, string>)field!.GetValue(registry)!;
    }

    #endregion

    [Fact]
    public async Task ReservedName_ThrowsMeasureException()
    {
        var ex = await Assert.ThrowsAsync<MeasureException>(async () =>
        {
            var def = new MeasureDefinition { Name = "_internal", Type = MeasureType.Number };
            var registry = CreateDisconnectedRegistry();
            await registry.RegisterAsync(def);
        });

        Assert.Contains("'_'", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InvalidUnit_ThrowsMeasureException()
    {
        await Assert.ThrowsAsync<MeasureException>(async () =>
        {
            var def = new MeasureDefinition
            {
                Name = "temp",
                Type = MeasureType.Number,
                QuantityType = "Temperature",
                Unit = "NotAValidUnit",
            };

            var registry = CreateDisconnectedRegistry();
            await registry.RegisterAsync(def);
        });
    }

    [Fact]
    public async Task ValidUnit_DoesNotThrow_DespiteRpcFailure()
    {
        var def = new MeasureDefinition
        {
            Name = "temp",
            Type = MeasureType.Number,
            QuantityType = "Temperature",
            Unit = "DegreeCelsius",
        };

        var registry = CreateDisconnectedRegistry();

        // Validation passes; store connection fails.
        var ex = await Assert.ThrowsAnyAsync<Exception>(async () => await registry.RegisterAsync(def));

        Assert.IsNotType<MeasureException>(ex);
    }

    [Fact]
    public async Task StringUnit_SkipsValidation_ThrowsNonMeasureException()
    {
        var def = new MeasureDefinition
        {
            Name = "label",
            Type = MeasureType.String,
            Unit = "Anything",
        };

        var registry = CreateDisconnectedRegistry();

        var ex = await Assert.ThrowsAnyAsync<Exception>(async () => await registry.RegisterAsync(def));

        Assert.IsNotType<MeasureException>(ex);
    }

    [Fact]
    public async Task Update_AtMinimum_Succeeds()
    {
        var store = new InMemoryStateStoreClient();
        var registry = new MeasureRegistry(store, Bucket, NullLogger<MeasureRegistry>.Instance);

        var def = new MeasureDefinition
        {
            Name = "n",
            Type = MeasureType.Number,
            QuantityType = "Scalar",
            Unit = "Amount",
            Minimum = 0,
        };

        await registry.RegisterAsync(def, initialValue: MeasureValue.Undefined);
        await registry.UpdateAsync("n", MeasureValue.FromValue(def, 0, DateTime.UtcNow));

        var json = store.GetStoredValueJson("n");
        Assert.NotNull(json);
    }

    [Fact]
    public async Task Update_AtMaximum_Succeeds()
    {
        var store = new InMemoryStateStoreClient();
        var registry = new MeasureRegistry(store, Bucket, NullLogger<MeasureRegistry>.Instance);

        var def = new MeasureDefinition
        {
            Name = "n",
            Type = MeasureType.Number,
            QuantityType = "Scalar",
            Unit = "Amount",
            Maximum = 10,
        };

        await registry.RegisterAsync(def, initialValue: MeasureValue.Undefined);
        await registry.UpdateAsync("n", MeasureValue.FromValue(def, 10, DateTime.UtcNow));

        var json = store.GetStoredValueJson("n");
        Assert.NotNull(json);
    }

    [Fact]
    public async Task Update_BelowMinimum_ThrowsMeasureException()
    {
        var store = new InMemoryStateStoreClient();
        var registry = new MeasureRegistry(store, Bucket, NullLogger<MeasureRegistry>.Instance);

        var def = new MeasureDefinition
        {
            Name = "n",
            Type = MeasureType.Number,
            QuantityType = "Scalar",
            Unit = "Amount",
            Minimum = 0,
        };

        await registry.RegisterAsync(def, initialValue: MeasureValue.Undefined);

        await Assert.ThrowsAsync<MeasureException>(async () =>
            await registry.UpdateAsync("n", MeasureValue.FromValue(def, -1, DateTime.UtcNow)));
    }

    [Fact]
    public async Task Update_AboveMaximum_ThrowsMeasureException()
    {
        var store = new InMemoryStateStoreClient();
        var registry = new MeasureRegistry(store, Bucket, NullLogger<MeasureRegistry>.Instance);

        var def = new MeasureDefinition
        {
            Name = "n",
            Type = MeasureType.Number,
            QuantityType = "Scalar",
            Unit = "Amount",
            Maximum = 10,
        };

        await registry.RegisterAsync(def, initialValue: MeasureValue.Undefined);

        await Assert.ThrowsAsync<MeasureException>(async () =>
            await registry.UpdateAsync("n", MeasureValue.FromValue(def, 11, DateTime.UtcNow)));
    }

    [Fact]
    public async Task Update_ConstantMeasure_ThrowsMeasureException()
    {
        var store = new InMemoryStateStoreClient();
        var registry = new MeasureRegistry(store, Bucket, NullLogger<MeasureRegistry>.Instance);

        var def = new MeasureDefinition
        {
            Name = "c",
            Type = MeasureType.Number,
            Attributes = MeasureAttributes.Constant,
            QuantityType = "Scalar",
            Unit = "Amount",
        };

        await registry.RegisterAsync(def, initialValue: MeasureValue.Undefined);

        await Assert.ThrowsAsync<MeasureException>(async () =>
            await registry.UpdateAsync("c", MeasureValue.FromValue(def, 1, DateTime.UtcNow)));
    }

    [Fact]
    public async Task Update_RespectsPrecision_RoundsStoredValue()
    {
        var store = new InMemoryStateStoreClient();
        var registry = new MeasureRegistry(store, Bucket, NullLogger<MeasureRegistry>.Instance);

        var def = new MeasureDefinition
        {
            Name = "n",
            Type = MeasureType.Number,
            QuantityType = "Scalar",
            Unit = "Amount",
            Precision = 2,
        };

        await registry.RegisterAsync(def, initialValue: MeasureValue.Undefined);

        await registry.UpdateAsync("n", MeasureValue.FromValue(def, 1.23456, DateTime.UtcNow));

        var json = store.GetStoredValueJson("n");
        Assert.NotNull(json);
        var readBack = MeasureJsonSerializer.DeserializeValue(def, json!);
        Assert.InRange(readBack.AsDouble(), 1.23 - 0.001, 1.24);
    }

    [Fact]
    public async Task CorrelationIdSet_AfterValueWrite_SingleUpdate()
    {
        var store = new InMemoryStateStoreClient
        {
            GateValueWrite = new TaskCompletionSource(),
            EnteredValueWrite = new TaskCompletionSource(),
        };

        var registry = new MeasureRegistry(store, Bucket, NullLogger<MeasureRegistry>.Instance);
        var def = new MeasureDefinition
        {
            Name = "m",
            Type = MeasureType.Number,
            QuantityType = "Scalar",
            Unit = "Amount",
        };
        store.GateValueWrite = null;
        store.EnteredValueWrite = null;
        await registry.RegisterAsync(def, initialValue: MeasureValue.Undefined);

        store.GateValueWrite = new TaskCompletionSource();
        store.EnteredValueWrite = new TaskCompletionSource();

        var pending = GetPendingCorrelations(registry);
        var updateTask = registry.UpdateAsync("m", MeasureValue.FromValue(def, 5, DateTime.UtcNow), "cid-1");
        if (store.EnteredValueWrite is not null)
        {
            await store.EnteredValueWrite.Task;
        }

        Assert.False(pending.ContainsKey("m"), "correlation must not be recorded before the store write finishes");

        if (store.GateValueWrite is not null)
        {
            store.GateValueWrite.TrySetResult();
        }

        await updateTask;
        Assert.Equal("cid-1", pending["m"]);
    }

    [Fact]
    public async Task CorrelationIdSet_AfterValueWrite_Batch()
    {
        var store = new InMemoryStateStoreClient
        {
            GateValueWrite = new TaskCompletionSource(),
            EnteredValueWrite = new TaskCompletionSource(),
        };

        var registry = new MeasureRegistry(store, Bucket, NullLogger<MeasureRegistry>.Instance);
        var a = new MeasureDefinition
        {
            Name = "a",
            Type = MeasureType.Number,
            QuantityType = "Scalar",
            Unit = "Amount",
        };
        var b = new MeasureDefinition
        {
            Name = "b",
            Type = MeasureType.Number,
            QuantityType = "Scalar",
            Unit = "Amount",
        };

        store.GateValueWrite = null;
        store.EnteredValueWrite = null;
        await registry.RegisterAsync(a, initialValue: MeasureValue.Undefined);
        await registry.RegisterAsync(b, initialValue: MeasureValue.Undefined);

        store.GateValueWrite = new TaskCompletionSource();
        store.EnteredValueWrite = new TaskCompletionSource();

        var pending = GetPendingCorrelations(registry);
        var t = registry.UpdateManyAsync(
        [
            ("a", MeasureValue.FromValue(a, 1, DateTime.UtcNow)),
            ("b", MeasureValue.FromValue(b, 2, DateTime.UtcNow)),
        ],
            "batch-1");
        if (store.EnteredValueWrite is not null)
        {
            await store.EnteredValueWrite.Task;
        }

        Assert.False(pending.ContainsKey("a"));
        Assert.False(pending.ContainsKey("b"));

        if (store.GateValueWrite is not null)
        {
            store.GateValueWrite.TrySetResult();
        }

        await t;
        Assert.Equal("batch-1", pending["a"]);
        Assert.Equal("batch-1", pending["b"]);
    }

    private static MeasureRegistry CreateDisconnectedRegistry()
    {
        var channel = Grpc.Net.Client.GrpcChannel.ForAddress("http://localhost:1");
        var client = new StateStore.StateStoreClient(channel);
        return new MeasureRegistry(client, Bucket, NullLogger<MeasureRegistry>.Instance);
    }
}
