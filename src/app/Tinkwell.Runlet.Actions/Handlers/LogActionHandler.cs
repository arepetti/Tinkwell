using Microsoft.Extensions.Logging;
using Tinkwell.Actions.Abstractions;
using Tinkwell.Configuration;
using Tinkwell.Configuration.Parser;
using Tinkwell.Events;
using Tinkwell.Expressions;

namespace Tinkwell.Runlet.Actions.Handlers;

/// <summary>
/// Built-in handler that logs a message when an action fires.
/// </summary>
/// <remarks>
/// Parameters:
/// <list type="bullet">
///   <item><c>message</c> (required) — the log message, supports expressions with <c>format()</c>.</item>
///   <item><c>level</c> (optional) — log level: trace, debug, information (default), warning, error, critical.</item>
/// </list>
/// </remarks>
internal sealed class LogActionHandler : IActionHandler
{
    private readonly ILogger<LogActionHandler> _logger;

    public LogActionHandler(ILogger<LogActionHandler> logger) => _logger = logger;

    public string Name => "log";

    public async Task ExecuteAsync(
        EventEnvelope trigger,
        IReadOnlyDictionary<string, ConfigValue> parameters,
        IExpressionEvaluator evaluator,
        CancellationToken cancellationToken)
    {
        var message = await ActionParameterResolver.ResolveRequiredAsync(
            "message", parameters, trigger, evaluator, cancellationToken);

        var levelStr = await ActionParameterResolver.ResolveOptionalAsync(
            "level", parameters, trigger, evaluator, cancellationToken);

        var level = ParseLevel(levelStr);

        _logger.Log(level, "[action] {Message}", message);
    }

    private static LogLevel ParseLevel(string? levelStr)
    {
        if (string.IsNullOrWhiteSpace(levelStr))
            return LogLevel.Information;

        return levelStr.Trim().ToLowerInvariant() switch
        {
            "trace" => LogLevel.Trace,
            "debug" => LogLevel.Debug,
            "information" or "info" => LogLevel.Information,
            "warning" or "warn" => LogLevel.Warning,
            "error" or "err" => LogLevel.Error,
            "critical" or "crit" => LogLevel.Critical,
            _ => LogLevel.Information,
        };
    }
}
