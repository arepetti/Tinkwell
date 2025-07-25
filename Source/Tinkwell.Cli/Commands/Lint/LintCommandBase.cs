using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Tinkwell.Cli.Commands.Lint;

public sealed class LintCommandSettings: CommandSettings
{
    [CommandArgument(0, "<PATH>")]
    [Description("Path of the file to lint")]
    public string Path { get; set; } = "";

    [CommandOption("-x|--exclude")]
    [Description("ID or category of a rule(s) to exclude (multiple")]
    public string[] Exclusions { get; set; } = [];

    [CommandOption("--strict")]
    [Description("Use stricter rules.")]
    public bool Strict { get; set; }

    [CommandOption("-v|--verbose")]
    [Description("Show a detailed output.")]
    public bool Verbose { get; set; }
}

abstract class LintCommandBase : AsyncCommand<LintCommandSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, LintCommandSettings settings)
    {
        var result = await AnsiConsole.Status()
            .Spinner(Spinner.Known.Default)
            .StartAsync("Linting...", async ctx =>
            {
                var linter = CreateLinter();
                linter.Exclusions = settings.Exclusions;
                return await linter.CheckAsync(settings.Path, settings.Strict);
            });

        if (settings.Verbose)
        {
            AnsiConsole.MarkupLine("\n[yellow]MESSAGES[/]");
            AnsiConsole.WriteLine();
            foreach (var message in result.Messages)
                AnsiConsole.WriteLine(message);

            AnsiConsole.MarkupLine("\n[yellow]APPLIED RULES[/]");
            var ruleTable = new SimpleTable("ID", "Category", "Strict", "Name");
            foreach (var rule in result.Rules)
                ruleTable.AddColoredRow(rule.IsStrict ? "white" : "silver", rule.Id, rule.Category, rule.IsStrict ? "Yes" : "No", rule.Name);

            AnsiConsole.Write(ruleTable.ToSpectreTable());
        }

        if (!result.Issues.Any())
        {
            AnsiConsole.MarkupLineInterpolated($"No issues found in [blueviolet]{settings.Path}[/].");
            return ExitCode.Ok;
        }

        var table = new SimpleTable("Rule", "Severity", "Target", "Name", "Description");
        foreach (var issue in result.Issues)
        {
            var color = IssueToColor(issue);
            table.AddColoredRow(
                color,
                issue.Id,
                issue.Severity.ToString(),
                issue.TargetType,
                issue.TargetName,
                issue.Message
            );
        }

        if (settings.Verbose)
            AnsiConsole.MarkupLine("\n[yellow]ISSUES[/]");

        AnsiConsole.Write(table.ToSpectreTable());

        return result.Ignorable ? ExitCode.Ok : ExitCode.Canceled;
    }

    protected abstract IFileLinter CreateLinter();

    private static string IssueToColor(Linter.Issue issue)
        => issue.Severity switch
        {
            Linter.IssueSeverity.Warning => "orange1",
            Linter.IssueSeverity.Error => "red",
            Linter.IssueSeverity.Critical => "red",
            _ => "silver",
        };
}
