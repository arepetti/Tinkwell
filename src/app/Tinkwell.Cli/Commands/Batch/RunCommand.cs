using System.ComponentModel;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using Spectre.Console.Cli;
using Tinkwell.Text;

namespace Tinkwell.Cli.Commands.Batch;

internal sealed class RunSettings : TwCoordinatorSettings
{
    [Description("Path to the script file to execute")]
    [CommandArgument(0, "<file>")]
    public string FilePath { get; set; } = "";

    [Description("Echo each command before executing")]
    [CommandOption("--echo")]
    [DefaultValue(false)]
    public bool Echo { get; set; }
}

[Description("Execute a batch script file")]
internal sealed class RunCommand : AsyncCommand<RunSettings>
{
    public override async Task<int> ExecuteAsync(
        CommandContext context, RunSettings settings, CancellationToken ct)
    {
        if (!File.Exists(settings.FilePath))
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] File not found: {Markup.Escape(settings.FilePath)}");
            return 1;
        }

        var allLines = await File.ReadAllLinesAsync(settings.FilePath, ct);

        var commands = new List<(int LineNumber, string Text)>();
        for (int i=0; i < allLines.Length; ++i)
        {
            if (!CommandLineTokenizer.IsBlankOrComment(allLines[i]))
                commands.Add((i + 1, allLines[i].Trim()));
        }

        if (commands.Count == 0)
        {
            if (!settings.NonInteractive)
                AnsiConsole.MarkupLine("[dim]No commands to execute.[/]");
            return 0;
        }

        var echo = settings.Echo && !settings.NonInteractive;
        var inheritedArgs = BuildInheritedArgs(settings);

        using var loggerFactory = LoggerFactory.Create(b => b.AddSimpleConsole());
        var extensionLoadLogger = loggerFactory.CreateLogger("Tinkwell.Cli.CommandLoader");

        for (int i=0; i < commands.Count; ++i)
        {
            var (lineNumber, text) = commands[i];
            var tokens = CommandLineTokenizer.Tokenize(text);
            if (tokens.Length == 0)
                continue;

            string[] fullArgs = [.. inheritedArgs, .. tokens];

            if (echo)
                AnsiConsole.MarkupLine(
                    $"[dim][[{i + 1}/{commands.Count}]][/] [blue]{Markup.Escape(text)}[/]");

            var app = new CommandApp();
            app.Configure(c => AppConfigurator.Configure(c, extensionLoadLogger));

            int exitCode;
            try
            {
                exitCode = await app.RunAsync(fullArgs);
            }
            catch (OutOfMemoryException) { Environment.FailFast("Out of memory"); throw; }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine(
                    $"[red]Error on line {lineNumber}:[/] {Markup.Escape(ex.Message)}");
                return 1;
            }

            if (exitCode != 0)
            {
                if (!settings.NonInteractive)
                    AnsiConsole.MarkupLine(
                        $"[red]Script aborted at line {lineNumber}:[/] {Markup.Escape(text)} [dim](exit code {exitCode})[/]");
                return exitCode;
            }
        }

        return 0;
    }

    private static string[] BuildInheritedArgs(RunSettings settings)
    {
        var args = new List<string>
        {
            "--pipe", settings.PipeName,
            "--machine", settings.Machine
        };

        if (settings.NonInteractive)
            args.Add("--non-interactive");

        if (settings.Verbose)
            args.Add("--verbose");

        return [.. args];
    }
}