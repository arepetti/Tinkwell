using Tinkwell.Measures;
using Tinkwell.Measures.Configuration.Parser;
using Tinkwell.Measures.Storage;
using Tinkwell.Services;
using Tinkwell.TestHelpers;

namespace Tinkwell.Reactor.Tests;

public class ReactorTests
{
    private readonly ReactorOptions _options = new() { Path = "test.twm", CheckOnStartup = true };

    [Fact]
    [Trait("Category", "CI-Disabled")]
    public async Task MeasureGoesAboveThreshold_PublishesEvent()
    {
        // Arrange
        TestInMemoryStorage storage = new();
        InMemoryStoreAdapter storeAdapter = new(storage);
        MockTwmFileReader fileReader = new();
        MockEventsGateway eventsGateway = new();
        MockLogger<Reactor> logger = new();
        Reactor reactor = new Reactor(logger, fileReader, storeAdapter, eventsGateway, _options);

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

        await storage.RegisterAsync(measure, new MeasureMetadata(DateTime.UtcNow), default);
        fileReader.AddSignal(signal);

        // Act - Initial check, undefined value.
        await reactor.StartAsync(default);
        await reactor.WaitForSubscriptionReadyAsync();

        // Act - Below threshold.
        await storeAdapter.WriteQuantityAsync(measure.Name, 90, default);
        await Task.Delay(100); // Give time for the change to propagate

        // Assert - No event yet
        Assert.Empty(eventsGateway.PublishedEvents);

        // Act - Above threshold
        await storeAdapter.WriteQuantityAsync(measure.Name, 101, default);
        await AsyncTestHelper.WaitForCondition(() => eventsGateway.PublishedEvents.Count == 1, failureMessage: "Event was not published after measure went above threshold");

        // Assert - Event published
        Assert.Single(eventsGateway.PublishedEvents);
        var publishedEvent = eventsGateway.PublishedEvents.First();
        Assert.Equal(signal.Topic, publishedEvent.Topic);
        Assert.Equal(measure.Name, publishedEvent.Subject);
        Assert.Equal(Verb.Triggered, publishedEvent.Verb);
        Assert.Equal(signal.Name, publishedEvent.Object);

        // Clean up
        await reactor.DisposeAsync();
    }

    [Fact]
    [Trait("Category", "CI-Disabled")]
    public async Task MeasureStartsAboveThreshold_PublishesEvent()
    {
        // Arrange
        TestInMemoryStorage storage = new();
        InMemoryStoreAdapter storeAdapter = new(storage);
        MockTwmFileReader fileReader = new();
        MockEventsGateway eventsGateway = new();
        MockLogger<Reactor> logger = new();
        Reactor reactor = new Reactor(logger, fileReader, storeAdapter, eventsGateway, _options);

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

        await storage.RegisterAsync(measure, new MeasureMetadata(DateTime.UtcNow), default);
        await storeAdapter.WriteQuantityAsync(measure.Name, 101, default);
        
        fileReader.AddSignal(signal);
        // Act - Initial check, above threshold
        await reactor.StartAsync(default);
        await reactor.WaitForSubscriptionReadyAsync();
        await AsyncTestHelper.WaitForCondition(() => eventsGateway.PublishedEvents.Count == 1, failureMessage: "Event was not published on startup when measure was already above threshold");

        // Assert - The event is already there
        Assert.Single(eventsGateway.PublishedEvents);

        // Clean up
        await reactor.DisposeAsync();
    }
}
