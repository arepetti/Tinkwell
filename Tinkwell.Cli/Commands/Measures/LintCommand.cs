using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using Tinkwell.Cli.Commands.Contracts;
using Tinkwell.Cli.Commands.Measures.Lint;

namespace Tinkwell.Cli.Commands.Measures;

[CommandFor("lint", parent: typeof(MeasuresCommand))]
[Description("Validate the content of a twm configuration file.")]
sealed class LintCommand : AsyncCommand<LintCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<PATH>")]
        [Description("Path of the file to lint")]
        public string Path { get; set; } = "";

        [CommandOption("-x|--exclude")]
        [Description("Name of a rule to exclude")]
        public string[] Exclusions { get; set; } = [];

        [CommandOption("--strict")]
        [Description("Use stricter rules.")]
        public bool Strict{ get; set; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        var issues = await AnsiConsole.Status()
            .Spinner(Spinner.Known.Default)
            .StartAsync("Linting...", async ctx =>
            {
                var linter = new TwmLinter();
                linter.Exclusions = settings.Exclusions;
                return await linter.CheckAsync(settings.Path);
            });

        if (!issues.Any())
        {
            AnsiConsole.MarkupLineInterpolated($"No issues found in [blue]{settings.Path}[/].");
            return ExitCode.Ok;
        }

        var table = new Table();
        table.Border = TableBorder.Simple;
        table.AddColumns("" +
            "[yellow]Rule[/]",
            "[yellow]Severity[/]",
            "[yellow]Target[/]",
            "[yellow]Name[/]",
            "[yellow]Description[/]"
        );

        foreach (var issue in issues)
        {
            var color = IssueToColor(issue);
            table.AddRow(
                $"[{color}]{issue.Id.EscapeMarkup()}[/]",
                $"[{color}]{issue.Severity.ToString().EscapeMarkup()}[/]",
                $"[{color}]{issue.TargetType.EscapeMarkup()}[/]",
                $"[{color}]{issue.TargetName.EscapeMarkup()}[/]",
                $"[{color}]{issue.Message.EscapeMarkup()}[/]"
            );
        }

        AnsiConsole.Write(table);

        bool ignorable = !settings.Strict && issues.All(x => x.Severity == Linter.IssueSeverity.Minor);
        return ignorable ? ExitCode.Ok : ExitCode.Canceled;
    }

    private static string IssueToColor(Linter.Issue issue)
        => issue.Severity switch
        {
            Linter.IssueSeverity.Warning => "orange1",
            Linter.IssueSeverity.Error => "red",
            Linter.IssueSeverity.Critical => "red",
            _ => "silver",
        };
}
