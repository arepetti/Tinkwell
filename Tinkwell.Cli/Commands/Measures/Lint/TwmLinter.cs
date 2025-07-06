using System.Diagnostics;
using Tinkwell.Measures.Configuration.Parser;

namespace Tinkwell.Cli.Commands.Measures.Lint;

sealed class TwmLinter : Linter<ITwmFile>
{
    protected override void LoadRules()
    {
        _rulesForMeasures = FindRules<ITwmLinterRule<MeasureDefinition>>();
        _rulesForSignals = FindRules<ITwmLinterRule<SignalDefinition>>();
    }

    protected override Task<ITwmFile> LoadFileAsync(string path)
    {
        var reader = new TwmFileReader();
        return reader.ReadFromFileAsync(path, CancellationToken.None);
    }

    protected override Result Lint(ITwmFile data)
    {
        Debug.Assert(_rulesForMeasures is not null);
        Debug.Assert(_rulesForSignals is not null);

        var result = new Result();
        result.Rules.AddRange(_rulesForMeasures.Cast<Rule>());
        result.Rules.AddRange(_rulesForSignals.Cast<Rule>());

        foreach (var measure in data.Measures)
            ApplyRulesTo(data, null, measure, result);

        foreach (var signal in data.Signals)
            ApplyRulesTo(data, null, signal, result);

        result.Messages.Add($"Applied {result.Rules.Count} rules.");
        result.Messages.Add($"Examined {_measureCount} measures.");
        result.Messages.Add($"Examined {_signalCount} signals.");

        return result;
    }

    private IEnumerable<ITwmLinterRule<MeasureDefinition>>? _rulesForMeasures;
    private IEnumerable<ITwmLinterRule<SignalDefinition>>? _rulesForSignals;
    private int _measureCount = 0;
    private int _signalCount = 0;

    private void ApplyRulesTo(ITwmFile file, object? parent, MeasureDefinition measure, Result result)
    {
        Debug.Assert(_rulesForMeasures is not null);
        
        ++_measureCount;

        foreach (var rule in _rulesForMeasures)
        {
            var issue = rule.Apply(file, null, measure);
            if (issue is not null)
                result.Issues.Add(issue);
        }

        foreach (var signal in measure.Signals)
            ApplyRulesTo(file, measure, signal, result);
    }

    private void ApplyRulesTo(ITwmFile file, object? parent, SignalDefinition signal, Result result)
    {
        Debug.Assert(_rulesForSignals is not null);

        ++_signalCount;

        foreach (var rule in _rulesForSignals)
        {
            var issue = rule.Apply(file, parent, signal);
            if (issue is not null)
                result.Issues.Add(issue);
        }
    }
}
