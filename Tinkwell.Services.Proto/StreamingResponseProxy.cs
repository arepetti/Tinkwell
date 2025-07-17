using Grpc.Core;

namespace Tinkwell;

sealed class StreamingResponseProxy<T>(AsyncServerStreamingCall<T> call) : IStreamingResponse<T>
{
    public IAsyncEnumerable<T> ReadAllAsync(CancellationToken cancellationToken)
        => _call.ResponseStream.ReadAllAsync(cancellationToken);
    public void Dispose()
        => _call.Dispose();

    private readonly AsyncServerStreamingCall<T> _call = call;
}
