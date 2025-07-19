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
    private MockTwaFileReader _fileReader = new();
    private readonly ExecutorOptions _options = new() { Path = "fake.twa" };
    private Executor _executor = default!;

    public ExecutorTests()
    {
        AgentsRecruiter.RegisterAssembly(typeof(ExecutorTests).Assembly);
    }

    public async Task InitializeAsync()
    {
        _fileReader = new MockTwaFileReader(
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
        _executor = new Executor(_logger, _eventsGateway, _fileReader, _dispatcher, _options);
        await _executor.StartAsync(CancellationToken.None);
    }

    public async Task DisposeAsync()
    {
        await _executor.DisposeAsync();
    }

    [Fact]
    public async Task StartAsync_WithValidConfig_SubscribesToCorrectEvents()
    {
        // Arrange

        // Act
        await AsyncTestHelper.WaitForCondition(() => _eventsGateway.Subscribers == 1, failureMessage: "Executor did not subscribe to events gateway on startup.");

        // Assert
        Assert.Equal(1, _eventsGateway.Subscribers);
    }

    [Fact]
    public async Task OnEventReceived_WithMatchingListener_DispatchesCorrectIntent()
    {
        // Arrange
        await Task.Delay(100);

        var eventToSend = new PublishEventsRequest
        {
            Topic = "test-topic",
            Payload = "{\"key\":\"value\"}"
        };

        // Act
        await _eventsGateway.PublishAsync(eventToSend, CancellationToken.None);
        await AsyncTestHelper.WaitForCondition(() => _dispatcher.Intents.Count == 1, failureMessage: "Intent was not dispatched after a matching event was published.");

        // Assert
        Assert.Single(_dispatcher.Intents);
        var intent = _dispatcher.Intents[0];
        Assert.Equal("test-topic", intent.Event.Topic);
        Assert.Equal("{\"key\":\"value\"}", intent.Payload);
        Assert.Equal(typeof(MockAgent), intent.Directive.AgentType);
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