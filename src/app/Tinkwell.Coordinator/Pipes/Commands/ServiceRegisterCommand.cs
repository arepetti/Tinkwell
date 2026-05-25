using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using Spectre.Console.Cli;
using Tinkwell;
using Tinkwell.Runner;

namespace Tinkwell.Coordinator.Pipes.Commands;

/// <summary>
/// Handles <c>service register runner-id json-array</c>.
/// Stores the service definitions reported by a runner so they are
/// discoverable via <c>service find</c> and <c>service list</c>.
/// </summary>
internal sealed class ServiceRegisterCommand : AsyncCommand<ServiceRegisterCommand.Settings>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private readonly PipeCommandContext _context;
    private readonly ILogger<ServiceRegisterCommand> _logger;

    public ServiceRegisterCommand(PipeCommandContext context, ILogger<ServiceRegisterCommand> logger)
    {
        _context = context;
        _logger = logger;
    }

    public override Task<int> ExecuteAsync(
        CommandContext spectreContext, Settings settings, CancellationToken cancellationToken)
    {
        var registry = _context.GetService<RunnerRegistry>();

        var runner = registry.FindById(settings.RunnerId);
        if (runner is null)
        {
            _context.WriteError($"unknown runner ID '{settings.RunnerId}'");
            return Task.FromResult(1);
        }

        List<ServiceDefinition> services;
        try
        {
            services = JsonSerializer.Deserialize<List<ServiceDefinition>>(settings.Json, JsonOptions)
                ?? [];
        }
        catch (JsonException ex)
        {
            _context.WriteError($"invalid JSON: {ex.Message}");
            return Task.FromResult(1);
        }

        runner.SetServices(services);

        _logger.LogInformation(
            "Runner '{Name}' (ID: {Id}) registered {Count} service(s)",
            runner.Config.Name, settings.RunnerId, services.Count);

        _context.WriteSuccess(new { Count = services.Count });
        return Task.FromResult(0);
    }

    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<runner-id>")]
        public string RunnerId { get; set; } = "";

        [CommandArgument(1, "<json>")]
        public string Json { get; set; } = "[]";

        public override ValidationResult Validate()
        {
            if (!ShortIdGenerator.IsValid(RunnerId))
                return ValidationResult.Error($"invalid runner ID '{RunnerId}'");

            if (string.IsNullOrWhiteSpace(Json))
                return ValidationResult.Error("JSON payload is required");

            return ValidationResult.Success();
        }
    }
}
