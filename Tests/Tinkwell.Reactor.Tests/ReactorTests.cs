using Tinkwell.Measures;
using Tinkwell.Measures.Configuration.Parser;
using Tinkwell.Measures.Storage;
using Tinkwell.Services;
using Tinkwell.TestHelpers;

namespace Tinkwell.Reactor.Tests;

public class ReactorTests : IAsyncLifetime
{
    private readonly TestInMemoryStorage _storage = new();
    private readonly InMemoryStoreAdapter _storeAdapter = new(new TestInMemoryStorage());
    private readonly MockTwmFileReader _fileReader = new();
    private readonly MockEventsGateway _eventsGateway = new();
    private readonly MockLogger<Reactor> _logger = new();
    private readonly ReactorOptions _options = new() { Path = "test.twm", CheckOnStartup = true };
    private Reactor _reactor = default!;

    public ReactorTests()
    {
        _storeAdapter = new InMemoryStoreAdapter(_storage);
    }

    public Task InitializeAsync()
    {
        _reactor = new Reactor(_logger, _fileReader, _storeAdapter, _eventsGateway, _options);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _reactor.DisposeAsync();
    }

    [Fact]
    [Trait("Category", "CI-Disabled")]
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

        // Act - Initial check, undefined value.
        await _reactor.StartAsync(default);
        await _reactor.WaitForSubscriptionReadyAsync();

        // Act - Below threshold.
        await _storeAdapter.WriteQuantityAsync(measure.Name, 90, default);
        await Task.Delay(100); // Give time for the change to propagate

        // Assert - No event yet
        Assert.Empty(_eventsGateway.PublishedEvents);

        // Act - Above threshold
        await _storeAdapter.WriteQuantityAsync(measure.Name, 101, default);
        await AsyncTestHelper.WaitForCondition(() => _eventsGateway.PublishedEvents.Count == 1, failureMessage: "Event was not published after measure went above threshold");

        // Assert - Event published
        Assert.Single(_eventsGateway.PublishedEvents);
        var publishedEvent = _eventsGateway.PublishedEvents.First();
        Assert.Equal(signal.Topic, publishedEvent.Topic);
        Assert.Equal(measure.Name, publishedEvent.Subject);
        Assert.Equal(Verb.Triggered, publishedEvent.Verb);
        Assert.Equal(signal.Name, publishedEvent.Object);
    }

    [Fact]
    [Trait("Category", "CI-Disabled")]
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
        // Act - Initial check, above threshold
        await _reactor.StartAsync(default);
        await _reactor.WaitForSubscriptionReadyAsync();
        await AsyncTestHelper.WaitForCondition(() => _eventsGateway.PublishedEvents.Count == 1, failureMessage: "Event was not published on startup when measure was already above threshold");

        // Assert - The event is already there
        Assert.Single(_eventsGateway.PublishedEvents);
    }
}
