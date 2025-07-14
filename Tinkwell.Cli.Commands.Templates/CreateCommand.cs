using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using Tinkwell.Cli.Commands.Templates.Manifest;

namespace Tinkwell.Cli.Commands.Templates;

[CommandFor("create", parent: typeof(TemplatesCommand))]
[Description("Creates a new template.")]
public sealed class CreateCommand : Command<CreateCommand.Settings>
{
    public sealed class Settings : TemplatesCommand.Settings
    {
        [CommandOption("-n|--no-examples")]
        [Description("Does not include extra settings as examples.")]
        public bool NoExamples { get; set; }
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        var template = new TemplateManifest();
        template.Id = Required("ID:");
        template.Name = Required("Name:");
        template.Description = Optional("Description:");
        template.Author = Optional("Author:");
        template.Copyright = $"Copyright (c) {template.Author} {DateTime.Now.Year}";
        template.Version = "1.0.0";

        var typesPrompt = new SelectionPrompt<string>()
            .Title("Type:")
            .AddChoices("standard", "meta");
        template.Type = AnsiConsole.Prompt(typesPrompt);

        if (settings.NoExamples == false)
            AddExamples(template);

        template.Save(settings.TemplatesDirectoryPath);

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLineInterpolated($"Created template [cyan]{template.Id}[/]");
        AnsiConsole.MarkupLineInterpolated($"Location: [blueviolet]{Path.GetDirectoryName(template.FullPath)}[/]");

        return ExitCode.Ok;
    }

    private static string Required(string label)
    {
        while (true)
        {
            string value = AnsiConsole.Ask<string>(label);
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }
    }

    private static string Optional(string label)
        => AnsiConsole.Ask<string>(label);

    private static void AddExamples(TemplateManifest template)
    {
        template.Questions.Add(new Question
        {
            Name = "text_example",
            Prompt = "Enter a name:",
            Type = "text",
            Default = "test"
        });

        if (template.Type == "standard")
        {
            template.Files.Add(new TemplateFile
            {
                Original = "configuration.tw",
                Target = "{{text_example}}",
                Mode = "copy"
            });
        }
        else
        {
            template.Sequence.Add(new SequenceStep
            {
                TemplateId = "another_template_id",
                When = "is_null_or_white_space({{text_example}}) == false"
            });
        }
    }
}
