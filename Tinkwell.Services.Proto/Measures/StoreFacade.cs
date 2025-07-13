using Google.Protobuf.WellKnownTypes;
using Tinkwell.Services;

namespace Tinkwell.Measures;

public sealed class StoreFacade
{
    internal StoreFacade(Store.StoreClient client)
    {
        _client = client;
    }

    public async Task WriteQuantityAsync(string name, double value, CancellationToken cancellationToken)
    {
        var request = new StoreUpdateRequest();
        request.Name = name;
        request.Value = value.ToStoreValue();
        await _client.UpdateAsync(request, cancellationToken: cancellationToken);
    }

    public async Task WriteQuantityAsync(string name, string value, CancellationToken cancellationToken)
    {
        var request = new StoreSetMeasureValueRequest();
        request.Name = name;
        request.Timestamp = Timestamp.FromDateTime(DateTime.UtcNow);
        request.ValueString = value;
        await _client.SetMeasureValueAsync(request, cancellationToken: cancellationToken);
    }

    public async Task WriteStringAsync(string name, string value, CancellationToken cancellationToken)
    {
        var request = new StoreUpdateRequest();
        request.Name = name;
        request.Value = value.ToStoreValue();
        await _client.UpdateAsync(request, cancellationToken: cancellationToken);
    }

    private readonly Store.StoreClient _client;
}