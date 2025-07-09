using Microsoft.Extensions.Logging;

namespace Tinkwell.Actions.Executor.Agents;

[Agent("log")]
public sealed class LogAgent(ILogger<LogAgent> logger) : IAgent
{
    public sealed class Settings
    {
        [AgentProperty("message")]
        public string Message { get; set; } = "";
    }

    object? IAgent.Settings => _settings;

    public Task ExecuteAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("{Message}", _settings.Message);
        return Task.CompletedTask;
    }

    private readonly ILogger<LogAgent> _logger = logger;
    private readonly Settings _settings = new();
}

