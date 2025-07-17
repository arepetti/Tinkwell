using Tinkwell.Measures;
using Tinkwell.Measures.Configuration.Parser;
using Tinkwell.Measures.Storage;
using Tinkwell.Services;
using Tinkwell.TestHelpers;

namespace Tinkwell.Reactor.Tests;

public class ReactorTests
{
    public ReactorTests()
    {
        _storage = new InMemoryStorage();
        _storeAdapter = new InMemoryStoreAdapter(_storage);
        _fileReader = new MockTwmFileReader();
        _eventsGateway = new MockEventsGateway();
        _logger = new MockLogger<Reactor>();
        _options = new ReactorOptions { Path = "test.twm", CheckOnStartup = true };
    }

    [Fact]
    public async Task MeasureGoesAboveThreshold_PublishesEvent()
    {
        // Arrange
        var measure = new Measures.MeasureDefinition { Name = "temperature", Type = MeasureType.Number, QuantityType = "Temperature", Unit = "DegreeCelsius" };
        var signal = new SignalDefinition
        {
            Name = "high_temperature",
            When = $"{measure.Name} > 100",
            Topic = "signal",
            Payload = new Dictionary<string, object>
            {
                { "subject", measure.Name },
                { "verb", "TRIGGERED" },
                { "object", "high_temperature" }
            }
        };

        await _storage.RegisterAsync(measure, new MeasureMetadata(DateTime.UtcNow), default);
        _fileReader.AddSignal(signal);

        var reactor = new Reactor(_logger, _fileReader, _storeAdapter, _eventsGateway, _options);

        // Act - Initial check, undefined value.
        await reactor.StartAsync(default);

        // Act - Below threshold.
        await _storeAdapter.WriteQuantityAsync(measure.Name, 90, default);
        await Task.Delay(100); // Give time for the change to propagate

        // Assert - No event yet
        Assert.Empty(_eventsGateway.PublishedEvents);

        // Act - Above threshold
        await _storeAdapter.WriteQuantityAsync(measure.Name, 101, default);
        await Task.Delay(100); // Give time for the change to propagate

        // Assert - Event published
        Assert.Single(_eventsGateway.PublishedEvents);
        var publishedEvent = _eventsGateway.PublishedEvents.First();
        Assert.Equal(signal.Topic, publishedEvent.Topic);
        Assert.Equal(measure.Name, publishedEvent.Subject);
        Assert.Equal(Verb.Triggered, publishedEvent.Verb);
        Assert.Equal(signal.Name, publishedEvent.Object);
    }

    [Fact]
    public async Task MeasureStartsAboveThreshold_PublishesEvent()
    {
        // Arrange
        var measure = new Measures.MeasureDefinition { Name = "temperature", Type = MeasureType.Number, QuantityType = "Temperature", Unit = "DegreeCelsius" };
        var signal = new SignalDefinition
        {
            Name = "high_temperature",
            When = $"{measure.Name} > 100",
            Topic = "signal",
            Payload = new Dictionary<string, object>
            {
                { "subject", measure.Name },
                { "verb", "TRIGGERED" },
                { "object", "high_temperature" }
            }
        };

        await _storage.RegisterAsync(measure, new MeasureMetadata(DateTime.UtcNow), default);
        await _storeAdapter.WriteQuantityAsync(measure.Name, 101, default);
        
        _fileReader.AddSignal(signal);
        var reactor = new Reactor(_logger, _fileReader, _storeAdapter, _eventsGateway, _options);

        // Act - Initial check, above threshold
        await reactor.StartAsync(default);
        await Task.Delay(100); // Give time for the change to propagate

        // Assert - The event is already there
        Assert.Single(_eventsGateway.PublishedEvents);
    }

    private readonly InMemoryStorage _storage;
    private readonly InMemoryStoreAdapter _storeAdapter;
    private readonly MockTwmFileReader _fileReader;
    private readonly MockEventsGateway _eventsGateway;
    private readonly MockLogger<Reactor> _logger;
    private readonly ReactorOptions _options;
}
