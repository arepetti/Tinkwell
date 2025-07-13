using System.ComponentModel;
using System.Text.Json;
using Spectre.Console;
using Spectre.Console.Cli;
using Tinkwell.Cli.Commands.Templates.Manifest;

namespace Tinkwell.Cli.Commands.Templates;

[CommandFor("list", parent: typeof(TemplatesCommand))]
[Description("List all the available templates.")]
public sealed class ListCommand : Command<TemplatesCommand.Settings>
{
    public override int Execute(CommandContext context, TemplatesCommand.Settings settings)
    {
        var table = new SimpleTable();
        table.AddColumns("Id", "Name", "Description");

        foreach (var directory in Directory.GetDirectories(settings.TemplateDirectoryPath))
        {
            var manifestPath = Path.Combine(directory, "template.json");
            if (File.Exists(manifestPath))
            {
                var manifest = JsonSerializer.Deserialize<TemplateManifest>(File.ReadAllText(manifestPath));
                if (manifest is not null)
                    table.AddRow(manifest.Id, manifest.Name, manifest.Description);
            }
        }

        AnsiConsole.Write(table);
        return 0;
    }
}