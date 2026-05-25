using Microsoft.Extensions.Logging;
using Spectre.Console;
using Spectre.Console.Cli;
using Tinkwell;
using Tinkwell.Runner;

namespace Tinkwell.Coordinator.Pipes.Commands;

/// <summary>
/// Handles <c>notify ready &lt;runner-id&gt;</c>.
/// Signals to the coordinator that the runner has completed initialization.
/// </summary>
internal sealed class NotifyReadyCommand : AsyncCommand<NotifyReadyCommand.Settings>
{
    private readonly PipeCommandContext _context;
    private readonly ILogger<NotifyReadyCommand> _logger;

    public NotifyReadyCommand(PipeCommandContext context, ILogger<NotifyReadyCommand> logger)
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

        _logger.LogInformation(
            "Runner '{Name}' (ID: {Id}) reported ready",
            runner.Config.Name, settings.RunnerId);

        runner.SignalReady();
        return Task.FromResult(0);
    }

    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<runner-id>")]
        public string RunnerId { get; set; } = "";

        public override ValidationResult Validate()
        {
            if (!ShortIdGenerator.IsValid(RunnerId))
                return ValidationResult.Error($"invalid runner ID '{RunnerId}'");

            return ValidationResult.Success();
        }
    }
}
