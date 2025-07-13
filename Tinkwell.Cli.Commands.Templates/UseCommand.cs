using System.ComponentModel;
using Spectre.Console.Cli;

namespace Tinkwell.Cli.Commands.Templates;

[CommandFor("use", parent: typeof(TemplatesCommand))]
[Description("Use a template to generate a new configuration/project.")]
public sealed class UseCommand : AsyncCommand<UseCommand.Settings>
{
    public sealed class Settings : TemplatesCommand.Settings
    { 
        [CommandArgument(0, "[TEMPLATE_ID]")]
        [Description("ID of the template to use.")]
        public string? TemplateId { get; set; }

        [CommandOption("-o|--output <PATH>")]
        [Description("The destination directory for the output files.")]
        public string OutputPath { get; set; } = Environment.CurrentDirectory;

        [CommandOption("-i|--input <FILE_PATH>")]
        [Description("Path to a JSON file with predefined answers.")]
        public string? InputFile { get; set; }

        [CommandOption("-s|--set <KEY=VALUE>")]
        [Description("Repeatable option to set/override a single answer.")]
        public string[] Set { get; set; } = [];

        [CommandOption("--unattended")]
        [Description("Disables interactive prompts; fails if an answer is missing.")]
        public bool Unattended { get; set; }

        [CommandOption("--dry-run")]
        [Description("Simulates the entire process without writing any files.")]
        public bool DryRun { get; set; }

        [CommandOption("-t|--trace <FILE_PATH>")]
        [Description("Saves the final, consolidated set of answers to a JSON file.")]
        public string? TraceFile { get; set; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        var engine = new TemplateEngine(settings);
        return await engine.ExecuteAsync();
    }
}
