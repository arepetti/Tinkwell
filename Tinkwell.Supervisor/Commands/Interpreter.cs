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
        app.Command("exit", exitCmd =>
        {
            exitCmd.OnExecute(() => TerminateParsingExitCode);
        });

        app.Command("runners", runnersCmd =>
        {
            runnersCmd.OnExecute(() => MissingArgumentsExitCode);

            runnersCmd.Command("list", listCmd =>
            {
                listCmd.OnExecute(() =>
                {
                    var queryArgument = listCmd.Argument("query", "");
                    writer.WriteLine(string.Join(',', FindAllByQuery(queryArgument.Value).Select(x => x.Definition.Name)));
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

                addCmd.OnExecute(() => {
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

                getCmd.OnExecute(() => {
                    var process = FindByNameOrId(nameArgument.Value?.Trim('"'), pidOption.Value());
                    writer.WriteLine(System.Text.Json.JsonSerializer.Serialize(process.Definition));
                });
            });
        });

        return app;

        void AddCommandOnRunner(CommandLineApplication parentCommand, string commandName, Action<IChildProcess> action)
        {
            parentCommand.Command(commandName, cmd =>
            {
                var nameArgument = cmd.Argument("name", "");
                var pidOption = cmd.Option("-p|--pid", "", CommandOptionType.SingleValue);

                cmd.OnExecute(() => {
                    var process = FindByNameOrId(nameArgument.Value?.Trim('"'), pidOption.Value());
                    action(process);
                    writer.WriteLine("OK");
                });
            });
        }
    }

    private IChildProcess FindByNameOrId(string? name, string? pid)
    {
        if (pid is null && name is null)
            throw new ArgumentException("You must specify either a name or a PID.");

        if (pid is not null)
        {
            if (!int.TryParse(pid, CultureInfo.InvariantCulture, out int id))
                throw new BootstrapperException($"Invalid PID '{pid}'.");

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

    private IEnumerable<IChildProcess> FindAllByQuery(string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return _runnerRegistry.Items;

        return _runnerRegistry.Items
            .Where(x => x.Definition.Name.Contains(query, StringComparison.OrdinalIgnoreCase));
    }
}
