using Microsoft.Extensions.Logging;
using Spectre.Console;
using Spectre.Console.Cli;
using Tinkwell.Coordinator.Pipes.Commands;
using Tinkwell.Pipes;
using Tinkwell.Telemetry;
using Tinkwell.Text;

namespace Tinkwell.Coordinator.Pipes;

/// <summary>
/// Dispatches pipe commands to individual Spectre.Console.Cli command classes.
/// Each incoming connection is a single command line; the dispatcher parses it,
/// routes it to the appropriate <see cref="AsyncCommand{TSettings}"/>, and
/// returns the response string.
/// </summary>
/// <remarks>
/// A fresh <see cref="CommandApp"/> and <see cref="PipeCommandContext"/> are
/// created per invocation so commands can write their response independently.
/// </remarks>
internal sealed class PipeCommandDispatcher
{
    private readonly IServiceProvider _services;
    private readonly ILogger<PipeCommandDispatcher> _logger;

    public PipeCommandDispatcher(IServiceProvider services, ILogger<PipeCommandDispatcher> logger)
    {
        _services = services;
        _logger = logger;
    }

    /// <summary>
    /// The <see cref="PipeConnectionHandler"/> delegate to pass to <see cref="PipeServer"/>.
    /// </summary>
    public async Task HandleConnectionAsync(PipeConnection connection, CancellationToken cancellationToken)
    {
        var line = await connection.ReadLineAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(line))
            return;

        _logger.LogTrace("Pipe command received: {Command}", line);

        var response = await DispatchAsync(line.Trim());
        await connection.WriteLineAsync(response, cancellationToken);
    }

    /// <summary>
    /// Parses and executes a single command line, returning the response string.
    /// </summary>
    public async Task<string> DispatchAsync(string commandLine)
    {
        using var span = OtTraces.Source.Timed(OtTraces.CommandDispatch, OtMetrics.CommandDuration);

        var commandName = commandLine.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault() ?? "unknown";
        span.SetTag(OtTraces.Command, commandName);

        var context = new PipeCommandContext(_services);

        var registrar = new SpectreTypeRegistrar(_services);
        registrar.RegisterInstance(typeof(PipeCommandContext), context);

        var app = new CommandApp(registrar);
        app.Configure(ConfigureCommands);

        var args = CommandLineTokenizer.Tokenize(commandLine);
        if (args.Length == 0)
            return PipeCommandContext.ErrorEnvelope("empty command");

        try
        {
            var exitCode = await app.RunAsync(args);

            if (exitCode != 0 && !context.HasExplicitResponse)
                return PipeCommandContext.ErrorEnvelope($"command failed (exit code {exitCode})");

            OtMetrics.CommandsDispatched.Inc(OtTraces.Command, commandName);
            return context.GetResponse();
        }
        catch (CommandParseException ex)
        {
            span.Error(ex.Message);
            return PipeCommandContext.ErrorEnvelope(ex.Message);
        }
        catch (CommandRuntimeException ex)
        {
            span.Error(ex.Message);
            return PipeCommandContext.ErrorEnvelope(ex.Message);
        }
        catch (OutOfMemoryException) { Environment.FailFast("Out of memory"); throw; }
        catch (Exception ex)
        {
            span.Error(ex.Message);
            _logger.LogError(ex, "Unhandled error dispatching command: {Command}", commandLine);
            return PipeCommandContext.ErrorEnvelope(ex.Message);
        }
    }

    /// <summary>
    /// Configures the Spectre command tree. Add new commands here as the
    /// protocol grows.
    /// </summary>
    private static void ConfigureCommands(IConfigurator config)
    {
        config.PropagateExceptions();
        config.Settings.Console = AnsiConsole.Create(
            new AnsiConsoleSettings { Out = new AnsiConsoleOutput(TextWriter.Null) });

        config.AddBranch("notify", notify =>
        {
            notify.AddCommand<NotifyReadyCommand>("ready");
            notify.AddCommand<NotifyFatalCommand>("fatal");
            notify.AddCommand<NotifyUnblockCommand>("unblock");
        });

        config.AddBranch("config", cfg =>
        {
            cfg.AddCommand<ConfigReadCommand>("read");
            cfg.AddCommand<ConfigPathCommand>("path");
        });

        config.AddBranch("runners", runners =>
        {
            runners.AddCommand<RunnersListCommand>("list");
        });

        config.AddBranch("endpoint", ep =>
        {
            ep.AddCommand<EndpointAllocateCommand>("allocate");
        });

        config.AddBranch("service", svc =>
        {
            svc.AddCommand<ServiceRegisterCommand>("register");
            svc.AddCommand<ServiceFindCommand>("find");
            svc.AddCommand<ServiceListCommand>("list");
        });

        config.AddCommand<QuitCommand>("quit");
    }
}