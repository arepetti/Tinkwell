using Microsoft.Extensions.Logging;
using Spectre.Console;
using Spectre.Console.Cli;
using Tinkwell;
using Tinkwell.Runner;

namespace Tinkwell.Coordinator.Pipes.Commands;

/// <summary>
/// Handles <c>notify fatal &lt;runner-id&gt; [message]</c>.
/// Signals that the runner encountered an unrecoverable error.
/// </summary>
internal sealed class NotifyFatalCommand : AsyncCommand<NotifyFatalCommand.Settings>
{
    private readonly PipeCommandContext _context;
    private readonly ILogger<NotifyFatalCommand> _logger;

    public NotifyFatalCommand(PipeCommandContext context, ILogger<NotifyFatalCommand> logger)
    {
        _context = context;
        _logger = logger;
    }

    public override Task<int> ExecuteAsync(CommandContext spectreContext, Settings settings, CancellationToken cancellationToken)
    {
        var registry = _context.GetService<RunnerRegistry>();

        var runner = registry.FindById(settings.RunnerId);
        if (runner is null)
        {
            _context.WriteError($"unknown runner ID '{settings.RunnerId}'");
            return Task.FromResult(1);
        }

        var message = string.IsNullOrWhiteSpace(settings.Message)
            ? "no details provided"
            : settings.Message;

        _logger.LogError(
            "Runner '{Name}' (ID: {Id}) reported fatal: {Message}",
            runner.Config.Name, settings.RunnerId, message);

        runner.SignalFatal(message);
        return Task.FromResult(0);
    }

    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<runner-id>")]
        public string RunnerId { get; set; } = "";

        [CommandArgument(1, "[message]")]
        public string? Message { get; set; }

        public override ValidationResult Validate()
        {
            if (!ShortIdGenerator.IsValid(RunnerId))
                return ValidationResult.Error($"invalid runner ID '{RunnerId}'");

            return ValidationResult.Success();
        }
    }
}
