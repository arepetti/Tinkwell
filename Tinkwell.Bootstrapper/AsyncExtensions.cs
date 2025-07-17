namespace Tinkwell.Bootstrapper;

/// <summary>
/// Extension methods for asynchronous operations.
/// </summary>
public static class AsyncExtensions
{
    /// <summary>
    /// <strong>Use with caution:</strong> consumes an asynchronous enumerable
    /// and returns all items as a list.
    /// </summary>
    /// <typeparam name="T">Type of elements to enumerate.</typeparam>
    /// <param name="source">Asynchronous enumeration to consume.</param>
    /// <param fileName="cancellationToken">A cancellation token to observe while waiting for the task to stop.</param>
    /// <returns>
    /// All the elements read from <paramref name="source"/>.
    /// </returns>
    /// <remarks>
    /// <strong>Warning</strong>: use this only if you know that the result is finite and it will
    /// fit entirely in memory! Consuming an asynchronous enumerable with an infinite (or big) source
    /// will eventually lead to an out-of-memory exception or cause the process to hang.
    /// </remarks>
    public static async Task<IEnumerable<T>> ConsumeAllAsync<T>(this IAsyncEnumerable<T> source, CancellationToken cancellationToken = default)
    {
        var result = new List<T>();
        await foreach (var item in source.WithCancellation(cancellationToken))
            result.Add(item);
        return result.AsEnumerable();
    }
}
