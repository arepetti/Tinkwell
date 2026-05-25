using Microsoft.Extensions.Logging;
using Spectre.Console;
using Spectre.Console.Cli;
using Tinkwell;
using Tinkwell.Configuration.Parser;
using Tinkwell.Runner;

namespace Tinkwell.Coordinator.Pipes.Commands;

/// <summary>
/// Handles <c>config read &lt;runner-id&gt;</c>.
/// Returns the list of runlet descriptors for the given runner as a JSON payload.
/// </summary>
internal sealed class ConfigReadCommand : AsyncCommand<ConfigReadCommand.Settings>
{
    private readonly PipeCommandContext _context;
    private readonly ILogger<ConfigReadCommand> _logger;

    public ConfigReadCommand(PipeCommandContext context, ILogger<ConfigReadCommand> logger)
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

        var runlets = runner.Config.Runlets
            .Select(r => new RunletDescriptor(
                r.Name,
                r.AssemblyPath,
                r.Options.ToDictionary(
                    kv => kv.Key,
                    kv => FlattenConfigValue(kv.Value))))
            .ToArray();

        var runnerSettings = runner.Config.Options.ToDictionary(
            kv => kv.Key,
            kv => FlattenConfigValue(kv.Value));

        _logger.LogTrace(
            "Serving config for runner '{Name}' (ID: {Id}): {Count} runlet(s), {SettingsCount} setting(s)",
            runner.Config.Name, settings.RunnerId, runlets.Length, runnerSettings.Count);

        _context.WriteSuccess(new
        {
            Name = runner.Config.Name,
            Settings = runnerSettings,
            Runlets = runlets
        });
        return Task.FromResult(0);
    }

    private static string FlattenConfigValue(ConfigValue value) => value switch
    {
        StringValue s => s.Value,
        LongValue l => l.Value.ToString(),
        DoubleValue d => d.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
        BoolValue b => b.Value ? "true" : "false",
        ExpressionValue e => e.Expression,
        _ => value.ToString()!
    };

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
