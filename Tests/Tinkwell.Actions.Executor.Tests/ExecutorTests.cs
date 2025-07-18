using Microsoft.Extensions.Logging;
using Tinkwell.Actions.Configuration.Parser;
using Tinkwell.Services;
using Tinkwell.TestHelpers;

namespace Tinkwell.Actions.Executor.Tests;

public class ExecutorTests
{
    public ExecutorTests()
    {
        AgentsRecruiter.RegisterAssembly(typeof(ExecutorTests).Assembly);
    }

    [Fact]
    public async Task StartAsync_WithValidConfig_SubscribesToCorrectEvents()
    {
        // Arrange
        var eventsGateway = new MockEventsGateway();
        var dispatcher = new MockIntentDispatcher();
        var logger = new LoggerFactory().CreateLogger<Executor>();
        var fileReader = new MockTwaFileReader(
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
            }
        );
        var options = new ExecutorOptions { Path = "fake.twa" };
        var executor = new Executor(logger, eventsGateway, fileReader, dispatcher, options);

        // Act
        await executor.StartAsync(CancellationToken.None);
        await Task.Delay(100);

        // Assert
        Assert.Equal(1, eventsGateway.Subscribers);
    }

    [Fact]
    public async Task OnEventReceived_WithMatchingListener_DispatchesCorrectIntent()
    {
        // Arrange
        var eventsGateway = new MockEventsGateway();
        var dispatcher = new MockIntentDispatcher();
        var logger = new LoggerFactory().CreateLogger<Executor>();
        var fileReader = new MockTwaFileReader(
            new WhenDefinition
            {
                Topic = "test-topic",
                Actions = new[] { new ActionDefinition("mock", new Dictionary<string, object>()) }.ToList()
            }
        );
        var options = new ExecutorOptions { Path = "fake.twa" };
        var executor = new Executor(logger, eventsGateway, fileReader, dispatcher, options);
        await executor.StartAsync(CancellationToken.None);
        await Task.Delay(100);

        var eventToSend = new PublishEventsRequest
        {
            Topic = "test-topic",
            Payload = "{\"key\":\"value\"}"
        };

        // Act
        await eventsGateway.PublishAsync(eventToSend, CancellationToken.None);
        await Task.Delay(100);

        // Assert
        Assert.Single(dispatcher.Intents);
        var intent = dispatcher.Intents[0];
        Assert.Equal("test-topic", intent.Event.Topic);
        Assert.Equal("{\"key\":\"value\"}", intent.Payload);
        Assert.Equal(typeof(MockAgent), intent.Directive.AgentType);
    }
}

file sealed class MockIntentDispatcher : IIntentDispatcher
{
    public List<Intent> Intents { get; } = new();

    public Task DispatchAsync(Intent intent, CancellationToken cancellationToken)
    { 
        Intents.Add(intent);
        return Task.CompletedTask;
    }
}