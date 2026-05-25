using Tinkwell.Runlet.Store.Grpc.V1;
using Tinkwell.Runner.Grpc;

namespace Tinkwell.Runner.Grpc.Tests;

public class GrpcNameResolverTests
{
    public sealed class TestStateStore : StateStore.StateStoreBase
    {
    }

    [Fact]
    public void Resolve_ReturnsProtoFullName_ForBoundService()
    {
        var name = GrpcNameResolver.Resolve(typeof(TestStateStore));
        Assert.Equal("tinkwell.store.v1.StateStore", name);
    }

    [Fact]
    public void Resolve_NonServiceType_Throws()
    {
        Assert.Throws<NotSupportedException>(() => GrpcNameResolver.Resolve(typeof(string)));
    }
}
