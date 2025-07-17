
using Google.Protobuf.WellKnownTypes;
using System.Threading.Channels;
using Tinkwell.Measures;
using Tinkwell.Measures.Storage;
using Tinkwell.Services;

namespace Tinkwell.TestHelpers;

public class InMemoryStoreAdapter : IStore
{
    public InMemoryStoreAdapter(IStorage storage)
    {
        _storage = storage;
        _storage.ValueChanged += OnValueChanged;
    }

    public int Subscribers { get; private set; } = 0;

    public async Task RegisterManyAsync(StoreRegisterManyRequest request, CancellationToken cancellationToken)
    {
        foreach (var item in request.Items)
        {
            var definition = new MeasureDefinition
            {
                Name = item.Definition.Name,
                Type = (MeasureType)item.Definition.Type,
                Unit = item.Definition.Unit,
                Minimum = item.Definition.Minimum,
                Maximum = item.Definition.Maximum,
                Precision = item.Definition.Precision,
                Attributes = (MeasureAttributes)item.Definition.Attributes,
                QuantityType = item.Definition.QuantityType,
                Ttl = item.Definition.Ttl?.ToTimeSpan(),
            };

            var metadata = new MeasureMetadata(DateTime.UtcNow)
            {
                Description = item.Metadata.Description,
                Category = item.Metadata.Category,
                Tags = item.Metadata.Tags,
            };

            await _storage.RegisterAsync(definition, metadata, cancellationToken);
        }
    }

    public async Task<StoreValueList> ReadManyAsync(IEnumerable<string> names, CancellationToken cancellationToken)
    {
        var measures = await _storage.FindAllAsync(names, cancellationToken);
        var values = new StoreValueList();
        foreach (var measure in measures)
        {
            values.Items.Add(new StoreValueList.Types.Item()
            {
                Name = measure.Definition.Name,
                Value = ToStoreValue(measure.Value)
            });
        }
        return values;
    }

    public async Task WriteQuantityAsync(string name, double value, CancellationToken cancellationToken)
    {
        var definition = _storage.FindDefinition(name);
        if (definition is null)
            throw new KeyNotFoundException($"Measure {name} does not exist");

        var measureValue = MeasureValue.FromValue(definition, value, DateTime.UtcNow);
        await _storage.UpdateAsync(name, measureValue, cancellationToken);
    }

    public Task WriteQuantityAsync(string name, string value, CancellationToken cancellationToken)
        => WriteStringAsync(name, value, cancellationToken);

    public async Task WriteStringAsync(string name, string value, CancellationToken cancellationToken)
    {
        var definition = _storage.FindDefinition(name);
        if (definition is null)
            throw new KeyNotFoundException($"Measure {name} does not exist");

        var measureValue = MeasureValue.FromValue(definition, value, DateTime.UtcNow);
        await _storage.UpdateAsync(name, measureValue, cancellationToken);
    }

    public ValueTask<IStreamingResponse<StoreValueChange>> SubscribeManyAsync(IEnumerable<string> names, CancellationToken cancellationToken)
    {
        ++Subscribers;
        var response = new StreamingResponse(_channel.Reader);
        return new ValueTask<IStreamingResponse<StoreValueChange>>(response);
    }

    private void OnValueChanged(object? sender, ValueChangedEventArgs e)
    {
        var change = new StoreValueChange
        {
            Name = e.Name,
            NewValue = ToStoreValue(e.NewValue),
        };

        if (e.OldValue is not null)
            change.OldValue = ToStoreValue(e.OldValue.Value);

        _channel.Writer.TryWrite(change);
    }

    public ValueTask DisposeAsync()
    {
        _storage.ValueChanged -= OnValueChanged;
        _channel.Writer.TryComplete();
        return ValueTask.CompletedTask;
    }

    private readonly IStorage _storage;
    private readonly Channel<StoreValueChange> _channel = Channel.CreateUnbounded<StoreValueChange>();

    private static StoreValue ToStoreValue(MeasureValue value)
    {
        var timestamp = Timestamp.FromDateTime(value.Timestamp);
        if (value.Type == MeasureValueType.Number)
        {
            return new StoreValue()
            {
                NumberValue = value.AsDouble(),
                Timestamp = timestamp,
            };
        }

        return new StoreValue()
        {
            StringValue = value.AsString(),
            Timestamp = timestamp,
        };
    }
}

file sealed class StreamingResponse(ChannelReader<StoreValueChange> reader) : IStreamingResponse<StoreValueChange>
{
    public IAsyncEnumerable<StoreValueChange> ReadAllAsync(CancellationToken cancellationToken = default)
        => _reader.ReadAllAsync(cancellationToken);

    public void Dispose() { }

    private readonly ChannelReader<StoreValueChange> _reader = reader;
}
