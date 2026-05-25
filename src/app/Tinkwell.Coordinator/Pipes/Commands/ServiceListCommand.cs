using Spectre.Console;
using Spectre.Console.Cli;

namespace Tinkwell.Coordinator.Pipes.Commands;

/// <summary>
/// Handles <c>service list [query]</c>.
/// Returns all registered services, optionally filtered by a query string
/// that matches against name, aliases, family, or friendly name.
/// </summary>
internal sealed class ServiceListCommand : AsyncCommand<ServiceListCommand.Settings>
{
    private readonly PipeCommandContext _context;

    public ServiceListCommand(PipeCommandContext context)
    {
        _context = context;
    }

    public override Task<int> ExecuteAsync(
        CommandContext spectreContext, Settings settings, CancellationToken cancellationToken)
    {
        var registry = _context.GetService<ServiceRegistry>();
        var services = registry.List(settings.Query);

        _context.WriteSuccess(new
        {
            Count = services.Count,
            Services = services.Select(s => new
            {
                s.Name,
                s.Type,
                s.FriendlyName,
                s.FamilyName,
                s.Aliases,
                s.Host,
                s.Url
            })
        });

        return Task.FromResult(0);
    }

    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "[query]")]
        public string? Query { get; set; }

        public override ValidationResult Validate() => ValidationResult.Success();
    }
}
