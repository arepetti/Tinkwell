using Microsoft.Extensions.Logging;
using Tinkwell.Actions.Configuration.Parser;
using Tinkwell.Services;
using Tinkwell.TestHelpers;

namespace Tinkwell.Actions.Executor.Tests;

public class ExecutorTests
{
    private readonly ExecutorOptions _options = new() { Path = "fake.twa" };

    public ExecutorTests()
    {
        AgentsRecruiter.RegisterAssembly(typeof(ExecutorTests).Assembly);
    }

    [Fact]
    [Trait("Category", "CI-Disabled")]
    public async Task StartAsync_WithValidConfig_SubscribesToCorrectEvents()
    {
        // Arrange
        MockEventsGateway eventsGateway = new();
        MockIntentDispatcher dispatcher = new();
        ILogger<Executor> logger = new LoggerFactory().CreateLogger<Executor>();
        MockTwaFileReader fileReader = new();

        fileReader = new MockTwaFileReader(
            new WhenDefinition
            {
                Topic = "topic1",
                Actions = new[] { new ActionDefinition("mock", new Dictionary<string, object>()) }.ToList()
            },
            new WhenDefinition
            {
                Topic = "topic2",
                Subject = "subject2",
                Actions = new[] { new ActionDefinition("mock", new Dictionary<string, object>()) }.ToList()
            },
            new WhenDefinition
            {
                Topic = "test-topic",
                Actions = new[] { new ActionDefinition("mock", new Dictionary<string, object>()) }.ToList()
            }
        );

        Executor executor = new Executor(logger, eventsGateway, fileReader, dispatcher, _options);
        await executor.StartAsync(CancellationToken.None);

        // Act
        await AsyncTestHelper.WaitForCondition(() => eventsGateway.Subscribers == 1, failureMessage: "Executor did not subscribe to events gateway on startup.");

        // Assert
        Assert.Equal(1, eventsGateway.Subscribers);

        // Clen up
        await executor.DisposeAsync();
    }

    [Fact]
    [Trait("Category", "CI-Disabled")]
    public async Task OnEventReceived_WithMatchingListener_DispatchesCorrectIntent()
    {
        // Arrange
        MockEventsGateway eventsGateway = new();
        MockIntentDispatcher dispatcher = new();
        ILogger<Executor> logger = new LoggerFactory().CreateLogger<Executor>();
        MockTwaFileReader fileReader = new();

        fileReader = new MockTwaFileReader(
            new WhenDefinition
            {
                Topic = "topic1",
                Actions = new[] { new ActionDefinition("mock", new Dictionary<string, object>()) }.ToList()
            },
            new WhenDefinition
            {
                Topic = "topic2",
                Subject = "subject2",
                Actions = new[] { new ActionDefinition("mock", new Dictionary<string, object>()) }.ToList()
            },
            new WhenDefinition
            {
                Topic = "test-topic",
                Actions = new[] { new ActionDefinition("mock", new Dictionary<string, object>()) }.ToList()
            }
        );

        Executor executor = new Executor(logger, eventsGateway, fileReader, dispatcher, _options);
        await executor.StartAsync(CancellationToken.None);

        var eventToSend = new PublishEventsRequest
        {
            Topic = "test-topic",
            Payload = "{\"key\":\"value\"}"
        };

        // Act
        await eventsGateway.PublishAsync(eventToSend, CancellationToken.None);
        await AsyncTestHelper.WaitForCondition(() => dispatcher.Intents.Count == 1, failureMessage: "Intent was not dispatched after a matching event was published.");

        // Assert
        Assert.Single(dispatcher.Intents);
        var intent = dispatcher.Intents[0];
        Assert.Equal("test-topic", intent.Event.Topic);
        Assert.Equal("{\"key\":\"value\"}", intent.Payload);
        Assert.Equal(typeof(MockAgent), intent.Directive.AgentType);

        // Clen up
        await executor.DisposeAsync();
    }
}

sealed class MockIntentDispatcher : IIntentDispatcher
{
    public List<Intent> Intents { get; } = new();

    public Task DispatchAsync(Intent intent, CancellationToken cancellationToken)
    { 
        Intents.Add(intent);
        return Task.CompletedTask;
    }
}