using System.ComponentModel;
using Spectre.Console.Cli;
using Tinkwell.Bootstrapper;

namespace Tinkwell.Cli.Commands.Templates;

[CommandFor("templates")]
[Description("Generates projects and configurations using templates.")]
public sealed class TemplatesCommand : Command<TemplatesCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [CommandOption("--path")]
        [Description("Path to the local directory containing the templates.")]
        public string TemplateDirectoryPath { get; set; }
            = Path.Combine(StrategyAssemblyLoader.GetEntryAssemblyDirectoryName(), "Templates");
    }

    public override int Execute(CommandContext context, Settings settings)
        => 0;
}