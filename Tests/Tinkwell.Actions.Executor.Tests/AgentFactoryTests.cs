using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Tinkwell.Actions.Configuration.Parser;

namespace Tinkwell.Actions.Executor.Tests;

public abstract class AgentFactoryTests
{
    [Fact]
    public void Create_WithValidIntent_CreatesAgentAndSetsProperties()
    {
        // Arrange
        var serviceProvider = new ServiceCollection().BuildServiceProvider();
        var factory = new AgentFactory();
        var intent = new Intent
        {
            Listener = new Listener { Id = "1", Topic = "test", Directives = new() },
            Event = new Intent.EventInfo { Id = "1", Topic = "test", Subject = "s", Verb = "v", Object = "o" },
            Directive = new Directive
            {
                AgentType = typeof(MockAgent),
                Parameters = new Dictionary<string, object>
                {
                    { "message", "Hello" },
                    { "value", 123 }
                }
            },
            Payload = "{}"
        };

        // Act
        var agent = factory.Create(serviceProvider, intent) as MockAgent;

        // Assert
        Assert.NotNull(agent);
        var settings = agent.Settings as MockAgent.MockAgentSettings;
        Assert.NotNull(settings);
        Assert.Equal("Hello", settings.Message);
        Assert.Equal(123, settings.Value);
    }

    [Fact]
    public void Create_WithPayload_ComposesProperties()
    {
        // Arrange
        var serviceProvider = new ServiceCollection().BuildServiceProvider();
        var factory = new AgentFactory();
        var intent = new Intent
        {
            Listener = new Listener { Id = "1", Topic = "test", Directives = new() },
            Event = new Intent.EventInfo { Id = "1", Topic = "test", Subject = "s", Verb = "v", Object = "o" },
            Directive = new Directive
            {
                AgentType = typeof(MockAgent),
                Parameters = new Dictionary<string, object>
                {
                    { "message", new ActionPropertyString(ActionPropertyStringKind.Template, "Hello {{ payload.name }}") },
                }
            },
            Payload = JsonSerializer.Serialize(new { name = "World" })
        };

        // Act
        var agent = factory.Create(serviceProvider, intent) as MockAgent;

        // Assert
        Assert.NotNull(agent);
        var settings = agent.Settings as MockAgent.MockAgentSettings;
        Assert.NotNull(settings);
        Assert.Equal("Hello World", settings.Message);
    }
}

[Agent("mock")]
public sealed class MockAgent : IAgent
{
    public object? Settings => _settings;
    private readonly MockAgentSettings _settings = new();

    public Task ExecuteAsync(CancellationToken cancellationToken)
        => Task.CompletedTask;

    public class MockAgentSettings
    {
        [AgentProperty("message")]
        public string Message { get; set; } = "";

        [AgentProperty("value")]
        public int Value { get; set; }
    }
}