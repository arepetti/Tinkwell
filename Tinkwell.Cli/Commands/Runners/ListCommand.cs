using System.ComponentModel;
using System.Text.RegularExpressions;
using Spectre.Console;
using Spectre.Console.Cli;
using Tinkwell.Bootstrapper.Ensamble;
using Tinkwell.Bootstrapper.Expressions;
using Tinkwell.Bootstrapper.Ipc;
using static Tinkwell.Cli.Commands.Runners.Profiler;

namespace Tinkwell.Cli.Commands.Runners;

[CommandFor("list", parent: typeof(RunnersCommand))]
[Description("List all the active runners.")]
sealed class ListCommand : AsyncCommand<ListCommand.Settings>
{
    public sealed class Settings : RunnersCommand.Settings
    {
        [CommandOption("--filter <NAME>")]
        [Description("The filter for the name of the root runners to include. You can use wildcards.")]
        public string Filter { get; set; } = "";

        [CommandOption("-v|--verbose <LEVEL>")]
        [Description("Show a detailed output.")]
        public bool Verbose { get; set; }

        [CommandOption("--columns")]
        [Description("Show the list (when not verbose) in columns.")]
        public bool Columns { get; set; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        using var client = new NamedPipeClient();

        string reply = await AnsiConsole.Status()
            .Spinner(Spinner.Known.Default)
            .StartAsync("Connecting...", async ctx =>
        {
                await client.ConnectAsync(settings.Machine, settings.Pipe, TimeSpan.FromSeconds(settings.Timeout));

                ctx.Status("Querying...");
                return await client.SendCommandAndWaitReplyAsync("runners list") ?? "";
        });

        if (reply.StartsWith("Error:"))
        {
            Consoles.Error.MarkupInterpolated($"The Supervisor replied with: [red]{reply}[/]");
            return ExitCode.Error;
        }

        var runners = reply.Split(',').ToArray();

        if (!string.IsNullOrWhiteSpace(settings.Filter))
        {
            var regex = new Regex(TextHelpers.GitLikeWildcardToRegex(settings.Filter), RegexOptions.IgnoreCase);
            runners = runners.Where(x => regex.IsMatch(x)).ToArray();
        }

        if (settings.Verbose)
        {
            var table = new Table();
            table.Border = TableBorder.Horizontal;
            table.AddColumns("[yellow]Name[/]", "[yellow]Path[/]", "[yellow]Firmlets[/]", "[yellow]Address[/]");

            await AnsiConsole.Status()
                .Spinner(Spinner.Known.Default)
                .StartAsync("Querying details...", async ctx =>
                {
                    for (int i = 0; i < runners.Length; ++i)
                    {
                        string name = runners[i];
                        string address = await client.SendCommandAndWaitReplyAsync($"endpoints query \"{name}\"") ?? "";
                        var info = await client.SendCommandAndWaitReplyAsync<RunnerDefinition>($"runners get \"{name}\"");

                        if (address.StartsWith("Error:"))
                            address = "";

                        table.AddRow(
                            $"[cyan]{name.EscapeMarkup()}[/]",
                            FormatPath(info?.Path ?? ""),
                            (info?.Children.Count ?? 0).ToString(),
                            $"[magenta]{address.EscapeMarkup()}[/]"
                        );

                        if (info is not null && info.Children.Count > 0)
                        {
                            foreach (var child in info.Children)
                                table.AddRow(
                                    $"[darkcyan]   {child.Name}[/]",
                                    "[white]   [/]" + FormatPath(child.Path)
                                );
                        }
                    }
                });

            AnsiConsole.Write(table);
        }
        else
        {
            if (settings.Columns)
                AnsiConsole.Write(new Columns(runners.Select(x => new Text(x, new Style(Color.Aqua)))));
            else
                AnsiConsole.Write(new Rows(runners.Select(x => new Text(x, new Style(Color.Aqua)))));
        }

        await client.SendCommandAsync("exit");
        return ExitCode.Ok;
    }

    private static readonly string DefaultTinkwellHostsPrefix = $"{nameof(Tinkwell)}.{nameof(Bootstrapper)}.";

    private static string FormatPath(string path)
    {
        var shortenedPath = Shorten(path);

        if (shortenedPath.StartsWith(nameof(Tinkwell)))
            return $"[grey]{nameof(Tinkwell)}.[/]{shortenedPath[(nameof(Tinkwell).Length + 1)..]}";

        return shortenedPath;

        static string Shorten(string text)
        {
            if (text.StartsWith(DefaultTinkwellHostsPrefix, StringComparison.Ordinal))
                return Trim($"[grey]...[/]{text[DefaultTinkwellHostsPrefix.Length..].EscapeMarkup()}");

            return Trim(text).EscapeMarkup();

            static string Trim(string s)
                => s.Length > 28 ? $"{s[..28]}..." : s;
        }
    }
}
