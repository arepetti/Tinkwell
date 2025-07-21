using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using Tinkwell.Cli.Commands.Templates.Manifest;

namespace Tinkwell.Cli.Commands.Templates;

[CommandFor("inspect", parent: typeof(TemplatesCommand))]
[Description("Inspect the specified template.")]
public sealed class InspectCommand : AsyncCommand<InspectCommand.Settings>
{
    public sealed class Settings : TemplatesCommand.Settings
    {
        [CommandArgument(0, "[TEMPLATE_ID]")]
        [Description("ID of the template to inspect.")]
        public string TemplateId { get; set; } = "";
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        string templateId = settings.TemplateId;
        if (string.IsNullOrWhiteSpace(templateId))
            templateId = await TemplateEngine.PromptToSelectTemplateAsync(settings.TemplatesDirectoryPath);

        var template = TemplateManifest.LoadFromId(settings.TemplatesDirectoryPath, templateId);

        var table = new PropertyValuesTable()
            .AddNameEntry("ID", template.Id)
            .AddEntry("Path", template.FullPath)
            .AddEntry("Last modified", new FileInfo(template.FullPath!).LastWriteTime)
            .AddEntry("Type", template.Type)
            .AddEntry("Version", template.Version)
            .AddEntry("Hidden", template.Hidden)
            .AddEntry("Name", template.Name)
            .AddEntry("Description", template.Description)
            .AddEntry("Author", template.Author)
            .AddEntry("Copyright", template.Copyright)
            .AddEntry("License", template.License)
            .AddEntry("Website", template.Webiste);

        if (template.Questions.Count > 0)
        {
            table.AddGroupTitle("Questions");
            foreach (var question in template.Questions)
                table.AddEntry(question.Name, question.Prompt, 1);
        }

        if (string.Equals(template.Type, "meta", StringComparison.OrdinalIgnoreCase))
        {
            table.AddGroupTitle("Sequence");
            foreach (var step in template.Sequence)
                table.AddEntry("Template ID", step.TemplateId, 1);
        }
        else
        {
            table.AddGroupTitle("Files");
            foreach (var file in template.Files)
                table.AddEntry(file.Mode, $"{file.Original} -> {file.Target}", 1);
        }


        AnsiConsole.Write(table);
        return ExitCode.Ok;
    }
}

