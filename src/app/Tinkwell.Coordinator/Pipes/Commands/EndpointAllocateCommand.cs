using System.Net;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using Spectre.Console.Cli;
using Tinkwell;

namespace Tinkwell.Coordinator.Pipes.Commands;

/// <summary>
/// Handles <c>endpoint allocate &lt;runner-id&gt; &lt;ip-address&gt;</c>.
/// Probes for an available port on the requested IP, assigns it to the
/// runner (stable across restarts), and returns the endpoint.
/// </summary>
internal sealed class EndpointAllocateCommand : AsyncCommand<EndpointAllocateCommand.Settings>
{
    private readonly PipeCommandContext _context;
    private readonly ILogger<EndpointAllocateCommand> _logger;

    public EndpointAllocateCommand(PipeCommandContext context, ILogger<EndpointAllocateCommand> logger)
    {
        _context = context;
        _logger = logger;
    }

    public override Task<int> ExecuteAsync(
        CommandContext spectreContext, Settings settings, CancellationToken cancellationToken)
    {
        var registry = _context.GetService<RunnerRegistry>();
        var allocator = _context.GetService<EndpointAllocator>();

        var runner = registry.FindById(settings.RunnerId);
        if (runner is null)
        {
            _context.WriteError($"unknown runner ID '{settings.RunnerId}'");
            return Task.FromResult(1);
        }

        if (!IPAddress.TryParse(settings.IpAddress, out var address))
        {
            _context.WriteError($"invalid IP address '{settings.IpAddress}'");
            return Task.FromResult(1);
        }

        IPEndPoint endpoint;
        try
        {
            endpoint = allocator.Allocate(runner.Config.Name, address);
        }
        catch (IOException ex)
        {
            _context.WriteError(ex.Message);
            return Task.FromResult(1);
        }

        runner.AssignEndpoint(endpoint);

        _logger.LogInformation(
            "Allocated endpoint {Endpoint} for runner '{Name}' (ID: {Id})",
            endpoint, runner.Config.Name, settings.RunnerId);

        _context.WriteSuccess(new
        {
            Ip = endpoint.Address.ToString(),
            Port = endpoint.Port
        });

        return Task.FromResult(0);
    }

    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<runner-id>")]
        public string RunnerId { get; set; } = "";

        [CommandArgument(1, "<ip-address>")]
        public string IpAddress { get; set; } = "";

        public override ValidationResult Validate()
        {
            if (!ShortIdGenerator.IsValid(RunnerId))
                return ValidationResult.Error($"invalid runner ID '{RunnerId}'");

            if (string.IsNullOrWhiteSpace(IpAddress))
                return ValidationResult.Error("IP address is required");

            return ValidationResult.Success();
        }
    }
}
