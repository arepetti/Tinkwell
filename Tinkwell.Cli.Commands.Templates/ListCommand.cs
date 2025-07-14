using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using Tinkwell.Cli.Commands.Templates.Manifest;

namespace Tinkwell.Cli.Commands.Templates;

[CommandFor("list", parent: typeof(TemplatesCommand))]
[Description("List all the available templates.")]
public sealed class ListCommand : Command<ListCommand.Settings>
{
    public sealed class Settings : TemplatesCommand.Settings
    {
        [CommandOption("-a|--all")]
        [Description("Shows all templates (including hidden ones).")]
        public bool All { get; set; }
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        var table = new SimpleTable();
        table.AddColumns("Id", "Name", "Description");

        foreach (var manifest in TemplateManifest.FindAll(settings.TemplatesDirectoryPath, settings.All))
            table.AddRow(manifest.Id, manifest.Name, manifest.Description);

        AnsiConsole.Write(table);
        return ExitCode.Ok;
    }
}

