namespace Tinkwell.Services.Proto.Proxies;

/// <summary>
/// A simplified interface for <see cref="Store.StoreClient"/>.
/// </summary>
public interface IStore : IAsyncDisposable
{
    /// <summary>
    /// Registers the specified set of measures.
    /// </summary>
    /// <param name="request">The request containing the definitios of the measures to register.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task RegisterManyAsync(StoreRegisterManyRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Reads the current value of a set of measures.
    /// </summary>
    /// <param name="names">The names of the measures to read.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>
    /// The current values for the measures with the specified names.
    /// </returns>
    Task<StoreValueList> ReadManyAsync(IEnumerable<string> names, CancellationToken cancellationToken);

    /// <summary>
    /// Writes a numeric (double) value to a measure.
    /// </summary>
    /// <param name="name">
    /// The name of the measure. This must be a numeric measure or a dynamic measure with the appropriate
    /// units already defined.
    /// </param>
    /// <param name="value">The numeric value to write.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task WriteQuantityAsync(string name, double value, CancellationToken cancellationToken);

    /// <summary>
    /// Writes a value to a measure from a string, letting the store handle the type conversion.
    /// </summary>
    /// <param name="name">The name of the measure.</param>
    /// <param name="value">
    /// The string representation of the value to write. This method performs value conversions
    /// (when possible) if the value is not with the same unit of the registered measure. It also
    /// performs some heuristics when there is a mismatch. It can be used also to update string measures.
    /// </param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task WriteQuantityAsync(string name, string value, CancellationToken cancellationToken);

    /// <summary>
    /// Writes a string value to a measure.
    /// </summary>
    /// <param name="name">
    /// The name of the measure. It must be a string or dynamic measure.
    /// </param>
    /// <param name="value">The string value to write.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task WriteStringAsync(string name, string value, CancellationToken cancellationToken);

    /// <summary>
    /// Subscribes to the changes of the specified set of measures.
    /// </summary>
    /// <param name="names">Names of the measures you want to watch for changes.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>
    /// The streaming interface you can use to enumerate asynchronously all the changes.
    /// </returns>
    ValueTask<IStreamingResponse<StoreValueChange>> SubscribeManyAsync(IEnumerable<string> names, CancellationToken cancellationToken);
}