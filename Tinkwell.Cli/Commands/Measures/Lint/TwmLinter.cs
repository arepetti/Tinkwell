using Tinkwell.Measures.Configuration.Parser;

namespace Tinkwell.Cli.Commands.Measures.Lint;

sealed class TwmLinter : Linter<ITwmFile>
{
    public TwmLinter()
    {
        _rulesForMeasures = FindRules<ITwmLinterRule<MeasureDefinition>>();
        _rulesForSignals = FindRules<ITwmLinterRule<SignalDefinition>>();
    }

    protected override Task<ITwmFile> LoadFileAsync(string path)
    {
        var reader = new TwmFileReader();
        return reader.ReadFromFileAsync(path, CancellationToken.None);
    }

    protected override void Lint(IList<Issue> issues, ITwmFile data)
    {
        foreach (var measure in data.Measures)
            ApplyRulesTo(data, null, measure, issues);

        foreach (var signal in data.Signals)
            ApplyRulesTo(data, null, signal, issues);
    }

    private readonly IEnumerable<ITwmLinterRule<MeasureDefinition>> _rulesForMeasures;
    private readonly IEnumerable<ITwmLinterRule<SignalDefinition>> _rulesForSignals;

    private void ApplyRulesTo(ITwmFile file, object? parent, MeasureDefinition measure, IList<Issue> issues)
    {
        foreach (var rule in _rulesForMeasures)
        {
            var issue = rule.Apply(file, null, measure);
            if (issue is not null)
                issues.Add(issue);
        }

        foreach (var signal in measure.Signals)
            ApplyRulesTo(file, measure, signal, issues);
    }

    private void ApplyRulesTo(ITwmFile file, object? parent, SignalDefinition signal, IList<Issue> issues)
    {
        foreach (var rule in _rulesForSignals)
        {
            var issue = rule.Apply(file, parent, signal);
            if (issue is not null)
                issues.Add(issue);
        }
    }
}
