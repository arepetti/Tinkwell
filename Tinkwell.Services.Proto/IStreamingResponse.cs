namespace Tinkwell;

/// <summary>
/// Represents the response of a gRPC call returning an asynchronous
/// stream of data.
/// </summary>
/// <typeparam name="T">The data type.</typeparam>
public interface IStreamingResponse<T> : IDisposable
{
    /// <summary>
    /// Enumerate all the available data and block waiting when none is available.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>
    /// An asynchronous enumerable object streaming the data.
    /// </returns>
    IAsyncEnumerable<T> ReadAllAsync(CancellationToken cancellationToken);
}
