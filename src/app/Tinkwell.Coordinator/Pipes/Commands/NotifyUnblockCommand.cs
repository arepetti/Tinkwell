using Microsoft.Extensions.Logging;
using Spectre.Console.Cli;

namespace Tinkwell.Coordinator.Pipes.Commands;

/// <summary>
/// Handles <c>notify unblock</c>.
/// Unblocks all runners currently waiting for <c>notify ready</c>
/// during the coordinator's startup sequence.
/// </summary>
internal sealed class NotifyUnblockCommand : AsyncCommand<NotifyUnblockCommand.Settings>
{
    private readonly PipeCommandContext _context;
    private readonly ILogger<NotifyUnblockCommand> _logger;

    public NotifyUnblockCommand(PipeCommandContext context, ILogger<NotifyUnblockCommand> logger)
    {
        _context = context;
        _logger = logger;
    }

    public override Task<int> ExecuteAsync(CommandContext spectreContext, Settings settings, CancellationToken cancellationToken)
    {
        var registry = _context.GetService<RunnerRegistry>();

        _logger.LogInformation("Received notify unblock — unblocking all pending runners");
        registry.UnblockAll();

        return Task.FromResult(0);
    }

    public sealed class Settings : CommandSettings;
}
