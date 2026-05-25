using Spectre.Console;

namespace Tinkwell.Cli.Commands.Init;

/// <summary>
/// Walks the question flow interactively, collecting answers into an
/// <see cref="AnswerBag"/>. Uses Spectre.Console prompts.
/// </summary>
internal sealed class WizardSession(IAnsiConsole console)
{
    public async Task<AnswerBag> RunAsync(
        QuestionFlow flow, CancellationToken cancellationToken = default)
    {
        var answers = new AnswerBag();

        foreach (var node in flow.Nodes)
        {
            cancellationToken.ThrowIfCancellationRequested();

            switch (node)
            {
                case QuestionDef question:
                    if (answers.EvaluateCondition(question.WhenCondition))
                        AskQuestion(question, answers);
                    break;

                case RepeatGroup group:
                    if (answers.EvaluateCondition(group.WhenCondition))
                        AskRepeatGroup(group, answers);
                    break;
            }
        }

        return await Task.FromResult(answers);
    }

    private void AskQuestion(QuestionDef question, AnswerBag answers)
    {
        switch (question.Type)
        {
            case QuestionType.Confirm:
                AskConfirm(question, answers);
                break;
            case QuestionType.Text:
                AskText(question, answers);
                break;
            case QuestionType.Integer:
                AskInteger(question, answers);
                break;
            case QuestionType.Choice:
                AskChoice(question, answers);
                break;
        }
    }

    private void AskConfirm(QuestionDef question, AnswerBag answers)
    {
        var descLines = ShowDescription(question.Description);

        var defaultValue = string.Equals(question.Default, "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(question.Default, "yes", StringComparison.OrdinalIgnoreCase);

        var result = console.Confirm(question.Prompt, defaultValue);
        answers.Set(question.Id, result);

        EraseDescription(descLines);
    }

    private void AskText(QuestionDef question, AnswerBag answers)
    {
        var descLines = ShowDescription(question.Description);

        var prompt = new TextPrompt<string>(question.Prompt + ":");
        if (question.Default is not null)
            prompt.DefaultValue(SubstituteIndex(question.Default, null));

        var result = console.Prompt(prompt);
        answers.Set(question.Id, result);

        EraseDescription(descLines);
    }

    private void AskInteger(QuestionDef question, AnswerBag answers)
    {
        var descLines = ShowDescription(question.Description);

        var prompt = new TextPrompt<int>(question.Prompt + ":");
        if (question.Default is not null && int.TryParse(question.Default, out var def))
            prompt.DefaultValue(def);

        var result = console.Prompt(prompt);
        answers.Set(question.Id, result);

        EraseDescription(descLines);
    }

    private void AskChoice(QuestionDef question, AnswerBag answers)
    {
        if (question.Options.Count == 0)
        {
            AskText(question, answers);
            return;
        }

        var descLines = ShowDescription(question.Description);

        var prompt = new SelectionPrompt<OptionDef>()
            .Title(question.Prompt)
            .UseConverter(o => o.Label);

        foreach (var option in question.Options)
            prompt.AddChoice(option);

        if (question.Default is not null)
        {
            var defaultOption = question.Options.FirstOrDefault(o =>
                string.Equals(o.Id, question.Default, StringComparison.OrdinalIgnoreCase));
            if (defaultOption is not null)
                prompt.HighlightStyle(new Style(Color.Cyan1));
        }

        var result = console.Prompt(prompt);
        answers.Set(question.Id, result.Id);

        EraseDescription(descLines);
    }

    /// <summary>
    /// Writes the description text below the current cursor position.
    /// Returns the number of lines written (0 if no description).
    /// </summary>
    private int ShowDescription(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
            return 0;

        console.MarkupLine($"[dim]{Markup.Escape(description)}[/]");
        return description.Split('\n').Length;
    }

    /// <summary>
    /// Moves the cursor up and clears the description lines that were
    /// shown before the prompt, so they disappear after the answer.
    /// </summary>
    private void EraseDescription(int lineCount)
    {
        if (lineCount <= 0)
            return;

        console.Cursor.MoveUp(lineCount);
        for (int i = 0; i < lineCount; i++)
        {
            console.Write(new string(' ', console.Profile.Width));
            if (i < lineCount - 1)
                console.Cursor.MoveDown(1);
        }
        console.Cursor.MoveUp(lineCount);
    }

    private void AskRepeatGroup(RepeatGroup group, AnswerBag answers)
    {
        console.MarkupLine($"[bold]{Markup.Escape(group.ItemLabel ?? group.ItemName)}[/]");

        var countPrompt = new TextPrompt<int>(group.Count.Prompt + ":")
            .DefaultValue(group.Count.Default)
            .Validate(n =>
            {
                if (n < group.Count.Minimum)
                    return ValidationResult.Error($"Minimum is {group.Count.Minimum}.");
                if (group.Count.Maximum.HasValue && n > group.Count.Maximum.Value)
                    return ValidationResult.Error($"Maximum is {group.Count.Maximum.Value}.");
                return ValidationResult.Success();
            });

        var count = console.Prompt(countPrompt);

        for (int i = 0; i < count; i++)
        {
            console.MarkupLine($"\n[dim]{Markup.Escape(group.ItemLabel ?? group.ItemName)} {i + 1}[/]");

            var item = new Dictionary<string, object>();

            foreach (var question in group.Questions)
            {
                if (!answers.EvaluateCondition(question.WhenCondition))
                    continue;

                var indexedQuestion = question with
                {
                    Prompt = SubstituteIndex(question.Prompt, i + 1),
                    Default = question.Default is not null
                        ? SubstituteIndex(question.Default, i + 1)
                        : null
                };

                var itemBag = new AnswerBag();
                AskQuestion(indexedQuestion, itemBag);
                var value = itemBag.Get(question.Id);
                if (value is not null)
                    item[question.Id] = value;
            }

            answers.AddRepeatItem(group.Id, item);
        }
    }

    private static string SubstituteIndex(string text, int? index)
    {
        if (index is null)
            return text;
        return text.Replace("{index}", index.Value.ToString());
    }
}
