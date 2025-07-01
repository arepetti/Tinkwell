using Grpc.Net.Client;

namespace Tinkwell;

public record GrpcService<T>(GrpcChannel Channel, T Client) : IDisposable, IAsyncDisposable
{
    public void Dispose()
        => DisposeAsync().GetAwaiter().GetResult();

    public async ValueTask DisposeAsync()
    {
        await Channel.ShutdownAsync();
        Channel.Dispose();
    }
}


