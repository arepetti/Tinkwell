using Spectre.Console.Cli;

namespace Tinkwell.Coordinator.Pipes.Commands;

/// <summary>
/// Handles <c>config path</c>.
/// Returns the fully-qualified path of the configuration file loaded at startup.
/// </summary>
internal sealed class ConfigPathCommand : AsyncCommand
{
    private readonly PipeCommandContext _context;

    public ConfigPathCommand(PipeCommandContext context)
    {
        _context = context;
    }

    public override Task<int> ExecuteAsync(CommandContext spectreContext, CancellationToken cancellationToken)
    {
        var info = _context.GetService<ConfigPathInfo>();

        _context.WriteSuccess(new { Path = info.Path });
        return Task.FromResult(0);
    }
}
