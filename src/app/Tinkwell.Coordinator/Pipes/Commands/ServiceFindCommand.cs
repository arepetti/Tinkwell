using Spectre.Console;
using Spectre.Console.Cli;

namespace Tinkwell.Coordinator.Pipes.Commands;

/// <summary>
/// Handles <c>service find &lt;name&gt;</c>.
/// Searches the coordinator's service registry by name, alias, and family,
/// returning the first match or an error if not found.
/// </summary>
internal sealed class ServiceFindCommand : AsyncCommand<ServiceFindCommand.Settings>
{
    private readonly PipeCommandContext _context;

    public ServiceFindCommand(PipeCommandContext context)
    {
        _context = context;
    }

    public override Task<int> ExecuteAsync(
        CommandContext spectreContext, Settings settings, CancellationToken cancellationToken)
    {
        var registry = _context.GetService<ServiceRegistry>();
        var service = registry.Find(settings.Name);

        if (service is null)
        {
            _context.WriteError($"service '{settings.Name}' not found");
            return Task.FromResult(1);
        }

        _context.WriteSuccess(new
        {
            service.Name,
            service.Type,
            service.FriendlyName,
            service.FamilyName,
            service.Aliases,
            service.Host,
            service.Url
        });

        return Task.FromResult(0);
    }

    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<name>")]
        public string Name { get; set; } = "";

        public override ValidationResult Validate()
        {
            if (string.IsNullOrWhiteSpace(Name))
                return ValidationResult.Error("service name is required");

            return ValidationResult.Success();
        }
    }
}
