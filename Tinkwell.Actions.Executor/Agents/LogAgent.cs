using Microsoft.Extensions.Logging;
using Tinkwell.Bootstrapper.Expressions;

namespace Tinkwell.Actions.Executor.Agents;

[Agent("log")]
public sealed class LogAgent(ILogger<LogAgent> logger) : IAgent
{
    public sealed class Settings
    {
        [AgentProperty("require")]
        public string Require { get; set; } = "";

        [AgentProperty("message")]
        public string Message { get; set; } = "";
    }

    object? IAgent.Settings => _settings;

    public Task ExecuteAsync(CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(_settings.Require))
        {
            var expr = new ExpressionEvaluator();
            if (expr.EvaluateBool(_settings.Require, null) == false)
                return Task.CompletedTask;
        }

        _logger.LogInformation("{Message}", _settings.Message);
        return Task.CompletedTask;
    }

    private readonly ILogger<LogAgent> _logger = logger;
    private readonly Settings _settings = new();
}
