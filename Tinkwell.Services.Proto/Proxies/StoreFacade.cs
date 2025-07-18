using Google.Protobuf.WellKnownTypes;

namespace Tinkwell.Services.Proto.Proxies;

/// <summary>
/// Provides a simplified facade over the <see cref="Store.StoreClient"/> for common measure operations.
/// </summary>
public sealed class StoreFacade
{
    internal StoreFacade(Store.StoreClient client)
    {
        _client = client;
    }

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
    public async Task WriteQuantityAsync(string name, double value, CancellationToken cancellationToken)
    {
        var request = new StoreUpdateRequest();
        request.Name = name;
        request.Value = value.ToStoreValue();
        await _client.UpdateAsync(request, cancellationToken: cancellationToken);
    }

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
    public async Task WriteQuantityAsync(string name, string value, CancellationToken cancellationToken)
    {
        var request = new StoreSetMeasureValueRequest();
        request.Name = name;
        request.Timestamp = Timestamp.FromDateTime(DateTime.UtcNow);
        request.ValueString = value;
        await _client.SetMeasureValueAsync(request, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Writes a string value to a measure.
    /// </summary>
    /// <param name="name">
    /// The name of the measure. It must be a string or dynamic measure.
    /// </param>
    /// <param name="value">The string value to write.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public async Task WriteStringAsync(string name, string value, CancellationToken cancellationToken)
    {
        var request = new StoreUpdateRequest();
        request.Name = name;
        request.Value = value.ToStoreValue();
        await _client.UpdateAsync(request, cancellationToken: cancellationToken);
    }

    private readonly Store.StoreClient _client;
}
