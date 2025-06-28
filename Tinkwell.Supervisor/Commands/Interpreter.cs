using McMaster.Extensions.CommandLineUtils;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Text.RegularExpressions;
using Tinkwell.Bootstrapper;

namespace Tinkwell.Supervisor.Commands;

sealed class Interpreter
{
    public enum ParsingResult
    {
        Continue,
        Stop,
    }

    public Interpreter(ILogger logger, IRegistry runnerRegistry)
    {
        _logger = logger;
        _runnerRegistry = runnerRegistry;
    }

    public event EventHandler<InterpreterResolveValueEventArgs>? ClaimRole;
    public event EventHandler<InterpreterResolveValueEventArgs>? QueryRole;
    public event EventHandler<InterpreterResolveValueEventArgs>? ClaimUrl;
    public event EventHandler<InterpreterResolveValueEventArgs>? QueryUrl;
    public event EventHandler? Signaled;

    public async Task<ParsingResult> ReadAndProcessNextCommandAsync(StreamReader reader, StreamWriter writer, CancellationToken cancellationToken)
    {
        string? input = (await reader.ReadLineAsync(cancellationToken))?.Trim();
        if (string.IsNullOrWhiteSpace(input) || input.StartsWith('#'))
            return ParsingResult.Continue;

        try
        {
            try
            {
                var app = ParseCommandLine(writer, input);
                int exitCode = await app.ExecuteAsync(TokenizeArguments());
                return exitCode == TerminateParsingExitCode ? ParsingResult.Stop : ParsingResult.Continue;

            }
            catch (UnrecognizedCommandParsingException e)
            {
                writer.WriteLine($"Error: {e.Message}");
            }
            catch (BootstrapperException e)
            {
                _logger.LogWarning(e, "Supervisor error while processing command: {Input}", input);
                writer.WriteLine($"Error: {e.Message}");
            }
            catch (ArgumentException e)
            {
                writer.WriteLine($"Error: {e.Message}");
            }
            catch (Exception e)
            {
                _logger.LogWarning(e, "Unexpected error while processing command: {Input}", input);
                writer.WriteLine("Error: unknown error.");
            }
        }
        catch (IOException)
        {
            // Cannot send the reply? The client probably disconnected
            return ParsingResult.Stop;
        }

        return ParsingResult.Continue;

        string[] TokenizeArguments()
        {
            var regex = new Regex(@"[\""].+?[\""]|[^ ]+");
            return [.. regex.Matches(input).Select(m => m.Value.Trim('"'))];
        }
    }

    private const int MissingArgumentsExitCode = 1;
    private const int TerminateParsingExitCode = 2;
    private readonly ILogger _logger;
    private readonly IRegistry _runnerRegistry;

    private CommandLineApplication ParseCommandLine(StreamWriter writer, string input)
    {
        var app = new CommandLineApplication();

        app.Command("ping", exitCmd =>
        {
            exitCmd.OnExecute(() => writer.WriteLine("OK"));
        });

        app.Command("exit", exitCmd =>
        {
            exitCmd.OnExecute(() => TerminateParsingExitCode);
        });

        app.Command("signal", signalCmd =>
        {
            var nameArgument = signalCmd.Argument("name", "");
            signalCmd.OnExecute(() =>
            {
                writer.WriteLine("OK");
                Signaled?.Invoke(this, EventArgs.Empty);
            });
        });

        SetupCommandRunners(writer, app);
        SetupCommandEndpoints(writer, app);
        SetupCommandRoles(writer, app);

        return app;
    }

    private void SetupCommandRunners(StreamWriter writer, CommandLineApplication app)
    {
        app.Command("runners", runnersCmd =>
        {
            runnersCmd.OnExecute(() => MissingArgumentsExitCode);

            runnersCmd.Command("list", listCmd =>
            {
                listCmd.OnExecute(() =>
                {
                    var queryArgument = listCmd.Argument("query", "");
                    writer.WriteLine(string.Join(',', _runnerRegistry.FindAllByQuery(queryArgument.Value).Select(x => x.Definition.Name)));
                });
            });

            AddCommandOnRunner(runnersCmd, "start", x => x.Start());
            AddCommandOnRunner(runnersCmd, "stop", x => x.Stop());
            AddCommandOnRunner(runnersCmd, "restart", x => x.Restart());

            runnersCmd.Command("add", addCmd =>
            {
                var nameArgument = addCmd.Argument("name", "");
                var pathArgument = addCmd.Argument("path", "");
                addCmd.AllowArgumentSeparator = true;

                addCmd.OnExecute(() =>
                {
                    _logger.LogInformation("Adding new runner {Path}", nameArgument.Value!.Trim('"'));
                    _runnerRegistry.AddNew(
                        nameArgument.Value!.Trim('"') ?? "",
                        pathArgument.Value?.Trim('"') ?? "",
                        string.Join(' ', addCmd.RemainingArguments));
                    writer.WriteLine("OK");
                });
            });

            runnersCmd.Command("get", getCmd =>
            {
                var nameArgument = getCmd.Argument("name", "");
                var pidOption = getCmd.Option("-p|--pid", "", CommandOptionType.SingleValue);

                getCmd.OnExecute(() =>
                {
                    var process = FindByNameOrId(nameArgument.Value?.Trim('"'), pidOption.Value());
                    writer.WriteLine(System.Text.Json.JsonSerializer.Serialize(process.Definition));
                });
            });
        });

        void AddCommandOnRunner(CommandLineApplication parentCommand, string commandName, Action<IChildProcess> action)
        {
            parentCommand.Command(commandName, cmd =>
            {
                var nameArgument = cmd.Argument("name", "");
                var pidOption = cmd.Option("-p|--pid", "", CommandOptionType.SingleValue);

                cmd.OnExecute(() =>
                {
                    var process = FindByNameOrId(nameArgument.Value?.Trim('"'), pidOption.Value());
                    action(process);
                    writer.WriteLine("OK");
                });
            });
        }
    }

    private void SetupCommandEndpoints(StreamWriter writer, CommandLineApplication app)
    {
        app.Command("endpoints", endpointsCmd =>
        {
            endpointsCmd.OnExecute(() => MissingArgumentsExitCode);

            endpointsCmd.Command("claim", claimCmd =>
            {
                var machineNameArgument = claimCmd.Argument("machine", "");
                var runnerNameArgument = claimCmd.Argument("runner", "");

                claimCmd.OnExecute(() =>
                {
                    ArgumentException.ThrowIfNullOrWhiteSpace(machineNameArgument.Value);
                    ArgumentException.ThrowIfNullOrWhiteSpace(runnerNameArgument.Value);

                    var ea = new InterpreterResolveValueEventArgs(machineNameArgument.Value!, runnerNameArgument.Value!);
                    ClaimUrl?.Invoke(this, ea);
                    if (string.IsNullOrWhiteSpace(ea.Value))
                        throw new ArgumentException($"Cannot claim an endpoint URL for '{ea.Runner}'.");

                    writer.WriteLine(ea.Value);
                });
            });

            endpointsCmd.Command("query", queryCmd =>
            {
                var name = queryCmd.Argument("name", "");
                var inverse = queryCmd.Option<bool>("-i|--inverse", "", CommandOptionType.SingleValue);

                queryCmd.OnExecute(() =>
                {
                    ArgumentException.ThrowIfNullOrWhiteSpace(name.Value);

                    var ea = new InterpreterResolveValueEventArgs("", name.Value!);
                    ea.Inverted = inverse.HasValue() && inverse.ParsedValue;
                    QueryUrl?.Invoke(this, ea);
                    if (string.IsNullOrWhiteSpace(ea.Value) && !ea.Inverted)
                        throw new ArgumentException($"Cannot query the endpoint for '{ea.Runner}'.");

                    writer.WriteLine(ea.Value ?? "");
                });
            });
        });
    }

    private void SetupCommandRoles(StreamWriter writer, CommandLineApplication app)
    {
        app.Command("roles", rolesCmd =>
        {
            rolesCmd.OnExecute(() => MissingArgumentsExitCode);

            rolesCmd.Command("claim", claimCmd =>
            {
                var roleArgument = claimCmd.Argument("role", "");
                var machineArgument = claimCmd.Argument("machine", "");
                var runnerNameArgument = claimCmd.Argument("runner", "");

                claimCmd.OnExecute(() =>
                {
                    ArgumentException.ThrowIfNullOrWhiteSpace(roleArgument.Value);
                    ArgumentException.ThrowIfNullOrWhiteSpace(machineArgument.Value);
                    ArgumentException.ThrowIfNullOrWhiteSpace(runnerNameArgument.Value);

                    var ea = new InterpreterResolveValueEventArgs(
                        roleArgument.Value,
                        machineArgument.Value,
                        runnerNameArgument.Value!);
                    ClaimRole?.Invoke(this, ea);

                    writer.WriteLine(ea.Value);
                });
            });

            rolesCmd.Command("query", queryRole =>
            {
                var roleArgument = queryRole.Argument("role", "");

                queryRole.OnExecute(() =>
                {
                    ArgumentException.ThrowIfNullOrWhiteSpace(roleArgument.Value);

                    var ea = new InterpreterResolveValueEventArgs(roleArgument.Value, "", "");
                    QueryRole?.Invoke(this, ea);

                    writer.WriteLine(ea.Value);
                });
            });
        });
    }

    private IChildProcess FindByNameOrId(string? name, string? pid)
    {
        if (pid is null && name is null)
            throw new ArgumentException("You must specify either a name or a PID.");

        if (pid is not null)
        {
            if (!int.TryParse(pid, CultureInfo.InvariantCulture, out int id))
                throw new ArgumentException($"Invalid PID '{pid}'.");

            var process = _runnerRegistry.FindById(id);
            if (process is not null)
                return process;
        }

        if (name is not null)
        {
            var process = _runnerRegistry.FindByName(name);
            if (process is not null)
                return process;
        }

        throw new ArgumentException($"Cannot find a runner with name '{name}' or PID '{pid}'.");
    }
}
