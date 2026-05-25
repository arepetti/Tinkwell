using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Spectre.Console.Cli;

namespace Tinkwell.Coordinator.Pipes.Commands;

/// <summary>
/// Handles <c>quit</c>.
/// Initiates a graceful shutdown of the coordinator process. Useful for
/// remote management and integration tests.
/// </summary>
internal sealed class QuitCommand : AsyncCommand<QuitCommand.Settings>
{
    private readonly PipeCommandContext _context;
    private readonly ILogger<QuitCommand> _logger;

    public QuitCommand(PipeCommandContext context, ILogger<QuitCommand> logger)
    {
        _context = context;
        _logger = logger;
    }

    public override Task<int> ExecuteAsync(
        CommandContext spectreContext, Settings settings, CancellationToken cancellationToken)
    {
        var lifetime = _context.GetService<IHostApplicationLifetime>();

        _logger.LogInformation("Received quit command — initiating graceful shutdown");
        _context.WriteSuccess("shutting down");

        lifetime.StopApplication();

        return Task.FromResult(0);
    }

    public sealed class Settings : CommandSettings;
}
