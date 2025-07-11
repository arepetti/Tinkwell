using Grpc.Net.Client;

namespace Tinkwell;

/// <summary>
/// Represents a gRPC service.
/// </summary>
/// <typeparam name="T">The type of the client class of the gRPC service.</typeparam>
/// <param name="Channel">Communication channel used by the service client.</param>
/// <param name="Client">The client of the gRPC service.</param>
public record GrpcService<T>(GrpcChannel Channel, T Client) : IDisposable, IAsyncDisposable
{
    /// <inheritdoc />
    public void Dispose()
        => DisposeAsync().GetAwaiter().GetResult();

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await Channel.ShutdownAsync();
        Channel.Dispose();
    }
}
