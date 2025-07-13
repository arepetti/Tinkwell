using System.Text.Json;
using Spectre.Console;
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
        LoadInitialInputs();

        // Select the template if not provided
        var templateId = _settings.TemplateId;
        if (string.IsNullOrEmpty(templateId))
            templateId = PromptToSelectTemplate();

        // Use the template to generate the output
        await ExecuteTemplateAsync(templateId);

        // Optionally save all the aggregated answers in one single file that can be used
        // with --input to exactly reproduce the same output (useful in combination with --unattended
        // to re-generate a standard configuration!)
        if (!string.IsNullOrEmpty(_settings.TraceFile))
        {
            await File.WriteAllTextAsync(
                _settings.TraceFile, 
                JsonSerializer.Serialize(_globalAnswers, new JsonSerializerOptions { WriteIndented = true })
            );
        }

        return 0;
    }

    private const string TemplateManifestFileName = "template.json";

    private readonly UseCommand.Settings _settings;
    private readonly Dictionary<string, object> _globalAnswers = new();
    private readonly TemplateRenderer _templateRenderer = new();

    // We need to know in which type of template we are. For "standard" templates the default action
    // for the template files is to copy them to the target, for "meta" templates (where we expect multiple
    // templates to contribute to the final configuration) is to append their content.
    // TODO: do we need to "merge" (somehow...) these files? It might be useful for code files.
    private string _currentTemplateType = "standard";


    // Load all the inputs from the various sources, the user won't be asked to answer for these
    private void LoadInitialInputs()
    {
        // The first step is to read the answers from the input file, if we have one.
        // This is our baseline which we override/extend with all the other inputs.
        if (!string.IsNullOrEmpty(_settings.InputFile))
        {
            var json = File.ReadAllText(_settings.InputFile);
            var options = new JsonSerializerOptions();
            options.Converters.Add(new ObjectJsonConverter());
            var inputData = JsonSerializer.Deserialize<Dictionary<string, object>>(json, options);
            if (inputData is not null)
            {
                foreach (var entry in inputData)
                    _globalAnswers[entry.Key] = entry.Value;
            }
        }

        // On the top of the baseline we set all the answers we received from the command line
        // in the form --set template_id.answer_id=value (where template_id can be omitted for
        // simple templates, it's used only in meta)
        foreach (var value in _settings.Set)
        {
            var parts = value.Split('=', 2);
            var keys = parts[0].Split('.');
            var currentDict = _globalAnswers;

            for (int i = 0; i < keys.Length - 1; i++)
            {
                if (!currentDict.ContainsKey(keys[i]) || !(currentDict[keys[i]] is Dictionary<string, object>))
                    currentDict[keys[i]] = new Dictionary<string, object>();
                currentDict = (Dictionary<string, object>)currentDict[keys[i]];
            }
            currentDict[keys[^1]] = parts[1];
        }
    }

    private string PromptToSelectTemplate()
    {
        // We give the user a nice list reading the manifest for all the available templates
        var templates = new List<TemplateManifest>();
        foreach (var directory in Directory.GetDirectories(_settings.TemplateDirectoryPath))
        {
            var manifestPath = Path.Combine(directory, TemplateManifestFileName);
            if (File.Exists(manifestPath))
            {
                var manifest = JsonSerializer.Deserialize<TemplateManifest>(File.ReadAllText(manifestPath));
                if (manifest is not null && !manifest.Hidden)
                    templates.Add(manifest);
            }
        }

        var prompt = new SelectionPrompt<string>()
            .Title("Select a template")
            .PageSize(10)
            .MoreChoicesText("[grey](Move up and down to reveal more templates)[/]")
            .AddChoices(templates.Select(t => t.Name));

        var selectedTemplateName = AnsiConsole.Prompt(prompt);
        return templates.First(t => t.Name == selectedTemplateName).Id;
    }

    // Core logic
    private async Task ExecuteTemplateAsync(string templateId)
    {
        var templateDirPath = Path.Combine(_settings.TemplateDirectoryPath, templateId);
        if (!Directory.Exists(templateDirPath))
            throw new DirectoryNotFoundException($"Template directory not found: {templateDirPath}");

        var manifestPath = Path.Combine(templateDirPath, TemplateManifestFileName);
        if (!File.Exists(manifestPath))
            throw new FileNotFoundException($"Template manifest not found: {manifestPath}");

        var manifest = JsonSerializer.Deserialize<TemplateManifest>(File.ReadAllText(manifestPath));
        if (manifest is null)
            throw new InvalidOperationException($"Could not deserialize template manifest: {manifestPath}");

        // Store current template type for default file mode logic (see _currentTemplatePath definition)
        var previousTemplateType = _currentTemplateType;
        _currentTemplateType = manifest.Type;

        // We have a set of global answers (from --input file or --set definitions), let's
        // apply them to the questions in the current template.
        var currentTemplateAnswers = new Dictionary<string, object>();
        if (_globalAnswers.ContainsKey(templateId) && _globalAnswers[templateId] is Dictionary<string, object> existingAnswers)
            currentTemplateAnswers = existingAnswers;
        else
            _globalAnswers[templateId] = currentTemplateAnswers;

        // Now let's ask the user to answer all the questions without a global answer
        foreach (var question in manifest.Questions)
        {
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

        _currentTemplateType = previousTemplateType; // Restore previous template type
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

    // Apply a standard template
    private async Task ProcessStandardTemplateAsync(string templateId, TemplateManifest manifest, Dictionary<string, object> answers)
    {
        var templateSourcePath = Path.Combine(_settings.TemplateDirectoryPath, templateId);
        var outputPath = _settings.OutputPath;

        // For each file from the template:
        // 1) read the original file content
        // 2) render the file content with Liquid passing the answers as model
        // 3) copy/append the rendered content to the output file
        await Parallel.ForEachAsync(manifest.Files, async (fileEntry, _) => {
            var originalFilePath = Path.Combine(templateSourcePath, fileEntry.Original);
            if (!File.Exists(originalFilePath))
            {
                // This is a configuration error in the template manifest
                AnsiConsole.MarkupLine($"[red]Error:[/] Original file not found: {originalFilePath}");
                return;
            }

            var renderedTargetFileName = _templateRenderer.Render(fileEntry.Target, answers);
            var destinationFilePath = Path.Combine(outputPath, renderedTargetFileName);

            // Se _currentTemplateType definition, the default action depend on the
            // type of template: when processing a meta it's "append" because we expect
            // (some) template to collaborate to generate the final file. When it's not the case
            // you can set an explicit override.
            var fileMode = fileEntry.Mode.ToLowerInvariant();
            if (fileMode == "unspecified")
                fileMode = _currentTemplateType == "meta" ? "append" : "copy";

            var content = await File.ReadAllTextAsync(originalFilePath);
            var renderedContent = _templateRenderer.Render(content, answers);

            if (_settings.DryRun)
            {
                AnsiConsole.MarkupLine($"[grey]{fileMode.ToUpperInvariant()} (DRY RUN):[/] [blueviolet]{destinationFilePath}[/]");
            }
            else
            {
                Directory.CreateDirectory(Path.GetDirectoryName(destinationFilePath)!);
                if (fileMode == "append" && File.Exists(destinationFilePath))
                    await File.AppendAllTextAsync(destinationFilePath, renderedContent);
                else // copy or append to non-existent file
                    await File.WriteAllTextAsync(destinationFilePath, renderedContent);

                AnsiConsole.MarkupLine($"{fileMode.ToUpperInvariant()}: [blueviolet]{destinationFilePath}[/]");
            }
        });
    }

    // Apply a meta template
    private async Task ProcessMetaTemplate(TemplateManifest manifest, Dictionary<string, object> answers)
    {
        var evaluator = new ExpressionEvaluator();
        // ExpressionEvaluator augments NCalc to support a dot-notation to access nested objects
        // but here we have dictionaries (which are not yet supported) so we need to flatten them
        // out. For example: "dictionary.nested_dictionary.value" is directly the parameter name.
        var flattenedAnswers = new Dictionary<string, object>();
        foreach (var templateEntry in _globalAnswers)
        {
            if (templateEntry.Value is Dictionary<string, object> templateSpecificAnswers)
            {
                foreach (var answerEntry in templateSpecificAnswers)
                    flattenedAnswers[$"{templateEntry.Key}.{answerEntry.Key}"] = answerEntry.Value;
            }
        }

        // A meta template is simply a list of templates to apply one after the other, let's do it
        foreach (var step in manifest.Sequence)
        {
            // Do we have a condition? Skip this sub-template if it's not satisfied.
            if (!string.IsNullOrEmpty(step.When) && !evaluator.EvaluateBool(step.When, flattenedAnswers))
                continue;

            await ExecuteTemplateAsync(step.TemplateId);
        }
    }
}
