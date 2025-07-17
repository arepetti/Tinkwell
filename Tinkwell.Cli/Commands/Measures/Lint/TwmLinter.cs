using System.Diagnostics;
using Tinkwell.Bootstrapper.Ensamble;
using Tinkwell.Cli.Commands.Lint;
using Tinkwell.Measures.Configuration.Parser;

namespace Tinkwell.Cli.Commands.Measures.Lint;

sealed class TwmLinter : Linter<ITwmFile>
{
    protected override void LoadRules()
    {
        _rulesForFile = FindRules<ITwmLinterRule<ITwmFile>>();
        _rulesForMeasures = FindRules<ITwmLinterRule<MeasureDefinition>>();
        _rulesForSignals = FindRules<ITwmLinterRule<SignalDefinition>>();
    }

    protected override Task<ITwmFile> LoadFileAsync(string path)
    {
        var reader = new TwmFileReader();
        return reader.ReadAsync(path, FileReaderOptions.Default, CancellationToken.None);
    }

    protected override Result Lint(ITwmFile data)
    {
        Debug.Assert(_rulesForFile is not null);
        Debug.Assert(_rulesForMeasures is not null);
        Debug.Assert(_rulesForSignals is not null);

        var result = new Result();
        result.Rules.AddRange(_rulesForFile.Cast<Rule>());
        result.Rules.AddRange(_rulesForMeasures.Cast<Rule>());
        result.Rules.AddRange(_rulesForSignals.Cast<Rule>());

        ApplyRulesTo(data, result);

        foreach (var measure in data.Measures)
            ApplyRulesTo(data, measure, result);

        foreach (var signal in data.Signals)
            ApplyRulesTo(data, null, signal, result);

        result.Messages.Add($"Examined 1 file.");
        result.Messages.Add($"Examined {_measureCount} measures.");
        result.Messages.Add($"Examined {_signalCount} signals.");
        result.Messages.Add($"Applied {result.Rules.Count} rules.");
        result.Messages.Add($"Found {result.Issues.Count} issues.");

        return result;
    }

    private IEnumerable<ITwmLinterRule<ITwmFile>>? _rulesForFile;
    private IEnumerable<ITwmLinterRule<MeasureDefinition>>? _rulesForMeasures;
    private IEnumerable<ITwmLinterRule<SignalDefinition>>? _rulesForSignals;
    private int _measureCount = 0;
    private int _signalCount = 0;

    private void ApplyRulesTo(ITwmFile file, Result result)
    {
        Debug.Assert(_rulesForFile is not null);

        foreach (var rule in _rulesForFile)
            result.Add(rule.Apply(file, null, file));
    }

    private void ApplyRulesTo(ITwmFile file, MeasureDefinition measure, Result result)
    {
        Debug.Assert(_rulesForMeasures is not null);
        
        ++_measureCount;

        foreach (var rule in _rulesForMeasures)
            result.Add(rule.Apply(file, null, measure));

        foreach (var signal in measure.Signals)
            ApplyRulesTo(file, measure, signal, result);
    }

    private void ApplyRulesTo(ITwmFile file, object? parent, SignalDefinition signal, Result result)
    {
        Debug.Assert(_rulesForSignals is not null);

        ++_signalCount;

        foreach (var rule in _rulesForSignals)
            result.Add(rule.Apply(file, parent, signal));
    }
}
