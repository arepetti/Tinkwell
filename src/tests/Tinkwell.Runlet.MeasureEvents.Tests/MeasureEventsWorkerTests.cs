using System.Threading.Channels;
using Microsoft.Extensions.Logging.Abstractions;
using Tinkwell.Runlet.Measures.Configuration;
using Tinkwell.Events;
using Tinkwell.Measures;
using Tinkwell.Runlet.Measures;
using Tinkwell.Runlet.MeasureEvents;

namespace Tinkwell.Runlet.MeasureEvents.Tests;

public class MeasureEventsWorkerTests
{
    [Fact]
    public async Task ValueChanged_PublishesEventWithCorrelationId()
    {
        var registry = new FakeMeasureRegistry();
        var def = new MeasureDefinition { Name = "voltage", Type = MeasureType.Number, QuantityType = "Scalar", Unit = "Amount" };
        await registry.RegisterAsync(def, initialValue: MeasureValue.FromValue(def, 0.0, DateTime.UtcNow));

        var registryHolder = new MeasureRegistryHolder();
        registryHolder.Set(registry);

        var configReady = new MeasuresConfigReady();
        configReady.Set(new MeasuresConfig([]));

        var published = new List<EventEnvelope>();
        var capturePublisher = new CapturePublisher(published);
        var publisherHolder = new EventPublisherHolder();
        publisherHolder.Set(capturePublisher);

        var options = new MeasureEventsOptions(
            new ChannelConfig(256, BoundedChannelFullMode.DropOldest));

        var worker = new MeasureEventsWorker(
            registryHolder, configReady, options, publisherHolder,
            NullLogger<MeasureEventsWorker>.Instance);

        using var cts = new CancellationTokenSource();
        var workerTask = worker.StartAsync(cts.Token);

        await Task.Delay(100);

        await registry.UpdateAsync("voltage",
            MeasureValue.FromValue(def, 230.0, DateTime.UtcNow), "corr001");

        await Task.Delay(200);
        await cts.CancelAsync();

        try
        {
            await workerTask;
        }
        catch (OperationCanceledException)
        {
        }

        Assert.NotEmpty(published);
        var evt = published.First(e => e.Name == "voltage");
        Assert.Equal("measures", evt.Source);
        Assert.Equal(EventVerb.Changed, evt.Verb);
        Assert.Equal("corr001", evt.CorrelationId);
    }

    [Fact]
    public async Task MultipleChanges_AllPublished()
    {
        var registry = new FakeMeasureRegistry();
        var defA = new MeasureDefinition { Name = "a", Type = MeasureType.Number, QuantityType = "Scalar", Unit = "Amount" };
        var defB = new MeasureDefinition { Name = "b", Type = MeasureType.Number, QuantityType = "Scalar", Unit = "Amount" };
        await registry.RegisterAsync(defA, initialValue: MeasureValue.FromValue(defA, 0.0, DateTime.UtcNow));
        await registry.RegisterAsync(defB, initialValue: MeasureValue.FromValue(defB, 0.0, DateTime.UtcNow));

        var registryHolder = new MeasureRegistryHolder();
        registryHolder.Set(registry);

        var configReady = new MeasuresConfigReady();
        configReady.Set(new MeasuresConfig([]));

        var published = new List<EventEnvelope>();
        var publisherHolder = new EventPublisherHolder();
        publisherHolder.Set(new CapturePublisher(published));

        var options = new MeasureEventsOptions(
            new ChannelConfig(256, BoundedChannelFullMode.DropOldest));

        var worker = new MeasureEventsWorker(
            registryHolder, configReady, options, publisherHolder,
            NullLogger<MeasureEventsWorker>.Instance);

        using var cts = new CancellationTokenSource();
        var workerTask = worker.StartAsync(cts.Token);

        await Task.Delay(100);

        await registry.UpdateAsync("a", MeasureValue.FromValue(defA, 1.0, DateTime.UtcNow));
        await registry.UpdateAsync("b", MeasureValue.FromValue(defB, 2.0, DateTime.UtcNow));

        await Task.Delay(200);
        await cts.CancelAsync();

        try
        {
            await workerTask;
        }
        catch (OperationCanceledException)
        {
        }

        Assert.True(published.Count >= 2);
        Assert.Contains(published, e => e.Name == "a");
        Assert.Contains(published, e => e.Name == "b");
    }

    private sealed class CapturePublisher : IEventPublisher
    {
        private readonly List<EventEnvelope> _events;

        public CapturePublisher(List<EventEnvelope> events) => _events = events;

        public Task PublishAsync(EventEnvelope envelope, CancellationToken ct = default)
        {
            _events.Add(envelope);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeMeasureRegistry : IMeasureRegistry
    {
        private readonly Dictionary<string, Measure> _measures = new(StringComparer.Ordinal);

        public event EventHandler<ValueChangedEventArgs>? ValueChanged;

        public Task RegisterAsync(MeasureDefinition definition, MeasureMetadata? metadata = null,
            MeasureValue? initialValue = null, CancellationToken ct = default)
        {
            _measures[definition.Name] = new Measure
            {
                Definition = definition,
                Metadata = metadata ?? new MeasureMetadata(),
                Value = initialValue ?? MeasureValue.Undefined,
            };
            return Task.CompletedTask;
        }

        public Task UpdateAsync(string name, MeasureValue value,
            string? correlationId = null, CancellationToken ct = default)
        {
            if (_measures.TryGetValue(name, out var existing))
            {
                var old = existing.Value;
                _measures[name] = existing with { Value = value };
                ValueChanged?.Invoke(this, new ValueChangedEventArgs
                {
                    Name = name,
                    OldValue = old,
                    NewValue = value,
                    CorrelationId = correlationId,
                });
            }
            return Task.CompletedTask;
        }

        public Task UpdateManyAsync(IEnumerable<(string Name, MeasureValue Value)> measures,
            string? correlationId = null, CancellationToken ct = default)
        {
            foreach (var (name, value) in measures)
                UpdateAsync(name, value, correlationId, ct).GetAwaiter().GetResult();
            return Task.CompletedTask;
        }

        public Task<Measure?> FindAsync(string name, CancellationToken ct = default) =>
            Task.FromResult(_measures.TryGetValue(name, out var m) ? m : null);

        public Task<IReadOnlyList<Measure>> FindAllAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Measure>>(_measures.Values.ToList());

        public Task<MeasureDefinition?> FindDefinitionAsync(string name, CancellationToken ct = default) =>
            Task.FromResult(_measures.TryGetValue(name, out var m) ? m.Definition : null);

        public Task WatchAsync(CancellationToken ct = default) =>
            Task.Delay(Timeout.Infinite, ct);
    }
}
