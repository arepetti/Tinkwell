using Grpc.Core;
using Sample.GrpcKeyValue.Grpc;

namespace Sample.GrpcKeyValue;

internal sealed class KeyValueGrpcService(InMemoryKeyValueStore store)
    : KeyValueStore.KeyValueStoreBase
{
    public override Task<GetResponse> Get(GetRequest request, ServerCallContext context)
    {
        var found = store.TryGet(request.Key, out var value);
        return Task.FromResult(new GetResponse
        {
            Value = value ?? string.Empty,
            Found = found,
        });
    }

    public override Task<SetResponse> Set(SetRequest request, ServerCallContext context)
    {
        store.Set(request.Key, request.Value);
        return Task.FromResult(new SetResponse());
    }
}
