using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using Tinkwell.Cli.Commands.Templates.Manifest;

namespace Tinkwell.Cli.Commands.Templates;

[CommandFor("list", parent: typeof(TemplatesCommand))]
[Description("List all the available templates.")]
public sealed class ListCommand : AsyncCommand<ListCommand.Settings>
{
    public sealed class Settings : TemplatesCommand.Settings
    {
        [CommandOption("-a|--all")]
        [Description("Shows all templates (including hidden ones).")]
        public bool All { get; set; }

        [CommandOption("--explain")]
        [Description("Explains where the templates are loaded from.")]
        public bool Explain { get; set; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        if (settings.Explain)
        {
            var sources = TemplateManifest
                .GetSearchPaths(settings.TemplatesDirectoryPath)
                .Where(x => !string.IsNullOrWhiteSpace(x));

            foreach (var source in sources)
            {
                if (Directory.Exists(source))
                    AnsiConsole.MarkupLineInterpolated($"Source: [blueviolet]{source}[/]");
                else
                    AnsiConsole.MarkupLineInterpolated($"Source: [gray]{source}[/]");
            }

        }

        var table = new SimpleTable();
        table.AddColumns("Id", "Name", "Version", "Author");

        foreach (var manifest in await TemplateManifest.FindAllAsync(settings.TemplatesDirectoryPath, settings.All))
        {
            table.AddUnescapedRow(
                $"[cyan]{manifest.Id.EscapeMarkup()}[/]",
                manifest.Name.EscapeMarkup(),
                manifest.Version.EscapeMarkup(),
                manifest.Author.EscapeMarkup()
            );
        }

        AnsiConsole.Write(table);
        return ExitCode.Ok;
    }
}

