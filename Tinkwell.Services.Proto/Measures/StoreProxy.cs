using Google.Protobuf.WellKnownTypes;
using Tinkwell.Services;

namespace Tinkwell.Measures;

/// <summary>
/// Provides a simplified facade over the <see cref="Store.StoreClient"/> for common measure operations.
/// </summary>
public sealed class StoreProxy(ServiceLocator locator) : IStore
{
    /// <inheritdocs />
    public async Task RegisterManyAsync(StoreRegisterManyRequest request, CancellationToken cancellationToken)
    {
        var client = await GetClient();
        await client.RegisterManyAsync(request, cancellationToken: cancellationToken);
    }

    public async Task<StoreValueList> ReadManyAsync(IEnumerable<string> names, CancellationToken cancellationToken)
    {
        var client = await GetClient();
        return await client.ReadManyAsync(new() { Names = { names } }, cancellationToken: cancellationToken);
    }

    /// <inheritdocs />
    public async Task WriteQuantityAsync(string name, double value, CancellationToken cancellationToken)
    {
        var request = new StoreUpdateRequest
        {
            Name = name,
            Value = value.ToStoreValue()
        };

        var client = await GetClient();
        await client.UpdateAsync(request, cancellationToken: cancellationToken);
    }

    /// <inheritdocs />
    public async Task WriteQuantityAsync(string name, string value, CancellationToken cancellationToken)
    {
        var request = new StoreSetMeasureValueRequest
        {
            Name = name,
            Timestamp = Timestamp.FromDateTime(DateTime.UtcNow),
            ValueString = value
        };

        var client = await GetClient();
        await client.SetMeasureValueAsync(request, cancellationToken: cancellationToken);
    }

    /// <inheritdocs />
    public async Task WriteStringAsync(string name, string value, CancellationToken cancellationToken)
    {
        var request = new StoreUpdateRequest
        {
            Name = name,
            Value = value.ToStoreValue()
        };

        var client = await GetClient();
        await client.UpdateAsync(request, cancellationToken: cancellationToken);
    }

    /// <inheritdocs />
    public async ValueTask<IStreamingResponse<StoreValueChange>> SubscribeManyAsync(IEnumerable<string> names, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(names, nameof(names));

        var request = new SubscribeManyRequest();
        request.Names.AddRange(names);

        var client = await GetClient();
        return new StreamingResponseProxy<StoreValueChange>(
            client.SubscribeMany(request, cancellationToken: cancellationToken));
    }

    /// <inheritdocs />
    public ValueTask DisposeAsync()
    {
        if (_store is not null)
            return ((IAsyncDisposable)_store).DisposeAsync();

        return ValueTask.CompletedTask;
    }

    private readonly ServiceLocator _locator = locator;
    private GrpcService<Store.StoreClient>? _store;

    private async Task<Store.StoreClient> GetClient()
    {
        if (_store is null)
            _store = await _locator.FindStoreAsync(CancellationToken.None);

        return _store.Client;
    }
}
