using Microsoft.Extensions.Logging;
using Tinkwell.Actions.Configuration.Parser;
using Tinkwell.Services;
using Tinkwell.TestHelpers;

namespace Tinkwell.Actions.Executor.Tests;

public class ExecutorTests : IAsyncLifetime
{
    private readonly MockEventsGateway _eventsGateway = new();
    private readonly MockIntentDispatcher _dispatcher = new();
    private readonly ILogger<Executor> _logger = new LoggerFactory().CreateLogger<Executor>();
    private readonly MockTwaFileReader _fileReader = new();
    private readonly ExecutorOptions _options = new() { Path = "fake.twa" };
    private Executor _executor = default!;

    public ExecutorTests()
    {
        AgentsRecruiter.RegisterAssembly(typeof(ExecutorTests).Assembly);
    }

    public Task InitializeAsync()
    {
        _executor = new Executor(_logger, _eventsGateway, _fileReader, _dispatcher, _options);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _executor.DisposeAsync();
    }

    [Fact]
    public async Task StartAsync_WithValidConfig_SubscribesToCorrectEvents()
    {
        // Arrange
        _fileReader.AddListener(
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

        // Act
        await _executor.StartAsync(CancellationToken.None);
        await AsyncTestHelper.WaitForCondition(() => eventsGateway.Subscribers == 1, failureMessage: "Executor did not subscribe to events gateway on startup.");

        // Assert
        Assert.Equal(1, eventsGateway.Subscribers);
    }

    [Fact]
    public async Task OnEventReceived_WithMatchingListener_DispatchesCorrectIntent()
    {
        // Arrange
        _fileReader.AddListener(
            new WhenDefinition
            {
                Topic = "test-topic",
                Actions = new[] { new ActionDefinition("mock", new Dictionary<string, object>()) }.ToList()
            }
        );
        await _executor.StartAsync(CancellationToken.None);
        await Task.Delay(100);

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