using System.Diagnostics;
using Tinkwell.Actions.Configuration.Parser;
using Tinkwell.Bootstrapper.Ensamble;
using Tinkwell.Bootstrapper.Expressions;
using Tinkwell.Cli.Commands.Ensamble.Lint;
using Tinkwell.Cli.Commands.Lint;

namespace Tinkwell.Cli.Commands.Actions.Lint;

sealed class ActionsLinter : Linter<ITwaFile>
{
    protected override void LoadRules()
    {
        _rulesForFile = FindRules<ITwaLinterRule<ITwaFile>>();
        _rulesForActions = FindRules<ITwaLinterRule<WhenDefinition>>();
    }

    protected override Task<ITwaFile> LoadFileAsync(string path)
    {
        // Note that we lint all the actions, even if they are not included. This could be a problem
        // if it's two actions with the same name are included but loaded on mutually exclusive conditions.
        // Unfortunately we cannot evaluate the conditions at this point.
        var conditionEvaluator = new EnsambleConditionEvaluator(null, new ExpressionEvaluator());
        var reader = new TwaFileReader(conditionEvaluator);
        return reader.ReadAsync(path, new FileReaderOptions(true), CancellationToken.None);
    }

    protected override Result Lint(ITwaFile data)
    {
        Debug.Assert(_rulesForFile is not null);
        Debug.Assert(_rulesForActions is not null);

        var result = new Result();
        result.Rules.AddRange(_rulesForFile.Cast<Rule>());
        result.Rules.AddRange(_rulesForActions.Cast<Rule>());

        ApplyRulesTo(data, result);

        foreach (var runner in data.Listeners)
            ApplyRulesTo(data, null, runner, result);

        result.Messages.Add($"Examined 1 file.");
        result.Messages.Add($"Examined {data.Listeners.Count()} actions.");
        result.Messages.Add($"Applied {result.Rules.Count} rules.");
        result.Messages.Add($"Found {result.Issues.Count} issues.");

        return result;
    }

    private IEnumerable<ITwaLinterRule<ITwaFile>>? _rulesForFile;
    private IEnumerable<ITwaLinterRule<WhenDefinition>>? _rulesForActions;

    private void ApplyRulesTo(ITwaFile file, Result result)
    {
        Debug.Assert(_rulesForFile is not null);

        foreach (var rule in _rulesForFile)
            result.Add(rule.Apply(file, null, file));
    }

    private void ApplyRulesTo(ITwaFile file, RunnerDefinition? parent, WhenDefinition when, Result result)
    {
        Debug.Assert(_rulesForActions is not null);

        foreach (var rule in _rulesForActions)
            result.Add(rule.Apply(file, null, when));
    }
}
