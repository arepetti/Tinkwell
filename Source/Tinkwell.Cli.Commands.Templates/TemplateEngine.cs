using Spectre.Console;
using System.Text;
using Tinkwell.Bootstrapper.Expressions;
using Tinkwell.Cli.Commands.Templates.Manifest;

namespace Tinkwell.Cli.Commands.Templates;

sealed class TemplateEngine
{
    public TemplateEngine(UseCommand.Settings settings)
    {
        _settings = settings;
    }

    public async Task<int> ExecuteAsync()
    {
        // Load initial inputs: they come from a trace file (for example created with --trace)
        // which is integrated with the (optional) values passed from the command line (with --set).
        // Any unanswered question is prompted to the user.
        await LoadInitialInputsAsync();

        // Select the template if not provided
        var templateId = _settings.TemplateId;
        if (string.IsNullOrEmpty(templateId))
            templateId = await PromptToSelectTemplateAsync(_settings.TemplatesDirectoryPath);

        // Use the template to generate the output
        await ExecuteTemplateAsync(templateId);

        // Optionally save all the aggregated answers in one single file that can be used
        // with --input to exactly reproduce the same output (useful in combination with --unattended
        // to re-generate a standard configuration!)
        if (!string.IsNullOrEmpty(_settings.TraceFile))
            await _answers.SaveAsync(_settings.TraceFile);

        return 0;
    }

    public static async Task<string> PromptToSelectTemplateAsync(string additionalTemplatesDirectoryPath)
    {
        // We give the user a nice list reading the manifest for all the available templates
        var templates = await TemplateManifest.FindAllAsync(additionalTemplatesDirectoryPath);
        var prompt = new SelectionPrompt<string>()
            .Title("Select a template")
            .PageSize(10)
            .MoreChoicesText("[grey](Move up and down to reveal more templates)[/]")
            .AddChoices(templates.Select(t => t.Name));

        var selectedTemplateName = AnsiConsole.Prompt(prompt);
        return templates.First(x => x.Name == selectedTemplateName).Id;
    }

    private readonly UseCommand.Settings _settings;
    private readonly TraceFile _answers = new();
    private readonly TemplateRenderer _templateRenderer = new();

    // We need to know in which type of template we are. For "standard" templates the default action
    // for the template files is to copy them to the target, for "meta" templates (where we expect multiple
    // templates to contribute to the final configuration) is to append their content.
    // TODO: do we need to "merge" (somehow...) these files? It might be useful for code files.
    private string? _rootTemplateType;


    // Load all the inputs from the various sources, the user won't be asked to answer for these
    private async Task LoadInitialInputsAsync()
    {
        // The first step is to read the answers from the input file, if we have one.
        // This is our baseline which we override/extend with all the other inputs.
        if (!string.IsNullOrEmpty(_settings.InputFile))
            await _answers.ImportAsync(_settings.InputFile);

        // On the top of the baseline we set all the answers we received from the command line
        // in the form --set template_id.answer_id=value
        _answers.Add(_settings.Set);
    }

    // Core logic
    private async Task ExecuteTemplateAsync(string templateId)
    {
        var manifest = TemplateManifest.LoadFromId(_settings.TemplatesDirectoryPath, templateId);
        AnsiConsole.MarkupLineInterpolated($"\nProcessing template [cyan]{manifest.Name}[/]");

        // Only the "entry" template determines the type
        if (string.IsNullOrEmpty(_rootTemplateType))
            _rootTemplateType = manifest.Type;

        // We have a set of global answers (from --input file or --set definitions), let's
        // apply them to the questions in the current template.
        var currentTemplateAnswers = _answers.GetAnswersFor(templateId);

        // Now let's ask the user to answer all the questions without a global answer
        foreach (var question in manifest.Questions)
        {
            if (!string.IsNullOrWhiteSpace(question.When))
            {
                var evaluator = new ExpressionEvaluator();
                bool isEnabled = evaluator.EvaluateBool(question.When, _answers.Flatten());
                if (!isEnabled)
                    continue;
            }

            if (!currentTemplateAnswers.ContainsKey(question.Name))
            {
                if (_settings.Unattended)
                    throw new InvalidOperationException($"Missing value for '{question.Name}' in unattended mode for template '{templateId}'.");

                currentTemplateAnswers[question.Name] = AskQuestion(question);
            }
        }

        // Processing differs according to the type of template: a standard template is a set
        // of things to do while a meta template is a list of standard templates (or meta...) to apply
        // in sequence (optionally filtererd with questions asked in the meta template). This enables
        // some reusing because we can move some parts to "shared" templates (which can even be marked
        // as TemplateManifest.Hidden to hide them from the list the user can select from).
        if (manifest.Type == "standard")
            await ProcessStandardTemplateAsync(templateId, manifest, currentTemplateAnswers);
        else if (manifest.Type == "meta")
            await ProcessMetaTemplate(manifest, currentTemplateAnswers);
    }

    private object AskQuestion(Question question)
    {
        switch (question.Type.ToLowerInvariant())
        {
            case "text":
                return AnsiConsole.Ask(question.Prompt, question.Default ?? "");
            case "confirm":
                return AnsiConsole.Confirm(question.Prompt, question.Default?.ToLowerInvariant() == "yes");
            case "selection":
                var prompt = new SelectionPrompt<string>()
                    .Title(question.Prompt)
                    .PageSize(10)
                    .AddChoices(question.Options);
                return AnsiConsole.Prompt(prompt);
            default:
                throw new InvalidOperationException($"Unknown question type: {question.Type}");
        }
    }

    // Apply a standard template (or a child template!)
    private async Task ProcessStandardTemplateAsync(string templateId, TemplateManifest manifest, Dictionary<string, object> answers)
    {
        var templateSourcePath = Path.GetDirectoryName(manifest.FullPath)!;
        var outputPath = _settings.OutputPath;
        bool isMetaTemplate = string.Equals(_rootTemplateType, "meta", StringComparison.OrdinalIgnoreCase);

        // Pre-process the list, we can't copy in parallel when appending
        foreach (var file in manifest.Files)
        {
            // See _rootTemplateType definition, the default action depend on the
            // type of template: when processing a meta it's "append" because we expect
            // (some) template to collaborate to generate the final file. When it's not the case
            // you can set an explicit override.
            if (string.Equals(file.Mode, "unspecified", StringComparison.OrdinalIgnoreCase))
                file.Mode = isMetaTemplate ? "append" : "copy";
        }

        bool canProcessInParallel = manifest.Files
            .All(file => string.Equals(file.Mode, "copy", StringComparison.OrdinalIgnoreCase));

        if (canProcessInParallel)
        {
            await Parallel.ForEachAsync(manifest.Files, async (file, _) =>
            {
                await CopyFileAsync(answers, file, templateSourcePath, outputPath);
            });
        }
        else
        {
            foreach (var file in manifest.Files)
                await CopyFileAsync(answers, file, templateSourcePath, outputPath);
        }
    }

    // Apply a meta template
    private async Task ProcessMetaTemplate(TemplateManifest manifest, Dictionary<string, object> answers)
    {
        // ExpressionEvaluator augments NCalc to support a dot-notation to access nested objects
        // but here we have dictionaries (which are not yet supported) so we need to flatten them
        // out. For example: "dictionary.nested_dictionary.value" is directly the parameter name.
        var evaluator = new ExpressionEvaluator();
        var flattenedAnswers = _answers.Flatten();

        // A meta template is simply a list of templates to apply one after the other, let's do it
        foreach (var step in manifest.Sequence)
        {
            // Do we have a condition? Skip this sub-template if it's not satisfied.
            if (!string.IsNullOrEmpty(step.When) && !evaluator.EvaluateBool(step.When, flattenedAnswers))
                continue;

            await ExecuteTemplateAsync(step.TemplateId);
        }
    }

    private async Task CopyFileAsync(Dictionary<string, object> answers, TemplateFile file, string templateSourcePath, string outputPath)
    {
        // Read the file raw content
        var originalFilePath = Path.Combine(templateSourcePath, file.Original);
        if (!File.Exists(originalFilePath))
        {
            // This is a configuration error in the template manifest
            AnsiConsole.MarkupLineInterpolated($"[red]Error:[/] Original file not found: {originalFilePath}");
            return;
        }

        // Render with Liquid to obtain the final content
        var content = await File.ReadAllTextAsync(originalFilePath, Encoding.UTF8);
        var renderedContent = _templateRenderer.Render(content, answers);

        // Write the final content into the output folder
        var renderedTargetFileName = _templateRenderer.Render(file.Target, answers);
        var destinationFilePath = Path.Combine(outputPath, renderedTargetFileName);

        if (_settings.DryRun)
        {
            AnsiConsole.MarkupLineInterpolated($"[grey]{file.Mode.ToUpperInvariant()} - DRY RUN:[/] [blueviolet]{destinationFilePath}[/]");
        }
        else
        {
            Directory.CreateDirectory(Path.GetDirectoryName(destinationFilePath)!);
            if (string.Equals(file.Mode, "append", StringComparison.OrdinalIgnoreCase) && File.Exists(destinationFilePath))
                await File.AppendAllTextAsync(destinationFilePath, renderedContent, Encoding.UTF8);
            else // copy or append to non-existent file
                await File.WriteAllTextAsync(destinationFilePath, renderedContent, Encoding.UTF8);

            AnsiConsole.MarkupLineInterpolated($"{file.Mode.ToUpperInvariant()}: [blueviolet]{destinationFilePath}[/]");
        }
    }
}
