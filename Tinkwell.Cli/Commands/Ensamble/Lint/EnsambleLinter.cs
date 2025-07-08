using System.Diagnostics;
using Tinkwell.Bootstrapper.Ensamble;
using Tinkwell.Bootstrapper.Expressions;
using Tinkwell.Cli.Commands.Lint;

namespace Tinkwell.Cli.Commands.Ensamble.Lint;

sealed class EnsambleLinter : Linter<IEnsambleFile>
{
    protected override void LoadRules()
    {
        _rulesForFile = FindRules<IEnsambleLinterRule<IEnsambleFile>>();
        _rulesForRunners = FindRules<IEnsambleLinterRule<RunnerDefinition>>();
    }

    protected override Task<IEnsambleFile> LoadFileAsync(string path)
    {
        // Note that we lint all the runners, even if they are not included. This could be a problem
        // if it's two runners with the same name are included but loaded on mutually exclusive conditions.
        // Unfortunately we cannot evaluate the conditions at this point.
        var conditionEvaluator = new EnsambleConditionEvaluator(null, new ExpressionEvaluator());
        var reader = new EnsambleFileReader(conditionEvaluator);
        return reader.ReadAsync(path, new FileReaderOptions(true), CancellationToken.None);
    }

    protected override Result Lint(IEnsambleFile data)
    {
        Debug.Assert(_rulesForFile is not null);
        Debug.Assert(_rulesForRunners is not null);

        var result = new Result();
        result.Rules.AddRange(_rulesForFile.Cast<Rule>());
        result.Rules.AddRange(_rulesForRunners.Cast<Rule>());

        ApplyRulesTo(data, result);

        foreach (var runner in data.Runners)
            ApplyRulesTo(data, null, runner, result);

        result.Messages.Add($"Examined 1 file.");
        result.Messages.Add($"Examined {data.Runners.Count()} runners.");
        result.Messages.Add($"Examined {_runnerCount - data.Runners.Count()} firmlets.");
        result.Messages.Add($"Applied {result.Rules.Count} rules.");
        result.Messages.Add($"Found {result.Issues.Count} issues.");

        return result;
    }

    private IEnumerable<IEnsambleLinterRule<IEnsambleFile>>? _rulesForFile;
    private IEnumerable<IEnsambleLinterRule<RunnerDefinition>>? _rulesForRunners;
    private int _runnerCount = 0;

    private void ApplyRulesTo(IEnsambleFile file, Result result)
    {
        Debug.Assert(_rulesForFile is not null);

        foreach (var rule in _rulesForFile)
            result.Add(rule.Apply(file, null, file));
    }

    private void ApplyRulesTo(IEnsambleFile file, RunnerDefinition? parent, RunnerDefinition runner, Result result)
    {
        Debug.Assert(_rulesForRunners is not null);

        ++_runnerCount;

        foreach (var rule in _rulesForRunners)
            result.Add(rule.Apply(file, null, runner));

        foreach (var child in runner.Children)
            ApplyRulesTo(file, runner, child, result);
    }
}
