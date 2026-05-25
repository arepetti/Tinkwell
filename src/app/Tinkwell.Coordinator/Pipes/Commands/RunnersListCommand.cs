using Microsoft.Extensions.Logging;
using Spectre.Console.Cli;

namespace Tinkwell.Coordinator.Pipes.Commands;

/// <summary>
/// Handles <c>runners list</c>.
/// Returns a snapshot of all runners with their IDs, PIDs, and statuses.
/// </summary>
internal sealed class RunnersListCommand : AsyncCommand<RunnersListCommand.Settings>
{
    private readonly PipeCommandContext _context;
    private readonly ILogger<RunnersListCommand> _logger;

    public RunnersListCommand(PipeCommandContext context, ILogger<RunnersListCommand> logger)
    {
        _context = context;
        _logger = logger;
    }

    public override Task<int> ExecuteAsync(CommandContext spectreContext, Settings settings, CancellationToken cancellationToken)
    {
        var registry = _context.GetService<RunnerRegistry>();
        var snapshot = registry.Snapshot();

        _logger.LogTrace("Serving runners list: {Count} runner(s)", snapshot.Count);

        _context.WriteSuccess(snapshot);
        return Task.FromResult(0);
    }

    public sealed class Settings : CommandSettings;
}
