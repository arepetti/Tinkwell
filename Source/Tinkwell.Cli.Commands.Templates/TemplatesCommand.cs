using System.ComponentModel;
using Spectre.Console.Cli;

namespace Tinkwell.Cli.Commands.Templates;

[CommandFor("templates")]
[Description("Generates projects and configurations using templates.")]
public sealed class TemplatesCommand : Command<TemplatesCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [CommandOption("--path")]
        [Description("Path to a local directory containing additional templates.")]
        public string TemplatesDirectoryPath { get; set; } = "";
    }

    public override int Execute(CommandContext context, Settings settings)
        => 0;
}