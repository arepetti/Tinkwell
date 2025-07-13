using Google.Protobuf.WellKnownTypes;
using System.Globalization;
using Tinkwell.Services;

namespace Tinkwell.Measures;

public static class StoreExtensions
{
    public static StoreValue ToStoreValue(this string value, DateTime? timestamp = null)
    {
        var storeValue = new StoreValue();
        storeValue.Timestamp = Timestamp.FromDateTime(timestamp ?? DateTime.UtcNow);
        storeValue.StringValue = value;
        return storeValue;
    }

    public static StoreValue ToStoreValue(this double value, DateTime? timestamp = null)
    {
        var storeValue = new StoreValue();
        storeValue.Timestamp = Timestamp.FromDateTime(timestamp ?? DateTime.UtcNow);
        storeValue.NumberValue = value;
        return storeValue;
    }

    public static object ToObject(this StoreValue value)
        => value.HasNumberValue ? value.NumberValue : value.StringValue;

    public static string FormatAsString(this StoreValue value)
    {
        if (value.HasNumberValue)
            return value.NumberValue.ToString("G", CultureInfo.InvariantCulture);

        return value.StringValue;
    }

    public static StoreFacade AsFacade(this Store.StoreClient client)
        => new StoreFacade(client);
}

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