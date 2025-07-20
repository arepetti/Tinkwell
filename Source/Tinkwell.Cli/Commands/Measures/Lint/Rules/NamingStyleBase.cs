using System.Text.RegularExpressions;
using Tinkwell.Cli.Commands.Lint;
using Tinkwell.Measures.Configuration.Parser;

namespace Tinkwell.Cli.Commands.Measures.Lint.Rules;

abstract class NamingStyleBase : Linter.Rule
{
    protected enum CaseStyle
    {
        CamelCase,
        PascalCase,
        SnakeCase,
        ScreamingSnakeCase,
        Unknown
    }

    protected IEnumerable<string> CollectAllIdentifiers(ITwmFile file)
    {
        var identifiersForMeasures = file.Measures.Select(x => x.Name);
        var identifiersForSignals = Enumerable.Concat(
            file.Measures.SelectMany(m => m.Signals.Select(s => s.Name)),
            file.Signals.Select(s => s.Name)
        );

        return Enumerable.Concat(identifiersForMeasures, identifiersForSignals).ToArray();
    }

    protected CaseStyle DetectDominantStyle(ITwmFile file)
        => DetectDominantStyle(CollectAllIdentifiers(file));

    protected static CaseStyle DetectDominantStyle(IEnumerable<string> identifiers)
    {
        var styleGroups = identifiers
            .Select(id => new { Identifier = id, Style = DetectStyle(id) })
            .GroupBy(x => x.Style)
            .OrderByDescending(g => g.Count())
            .ToList();

        return styleGroups.FirstOrDefault(g => g.Key != CaseStyle.Unknown)?.Key ?? CaseStyle.Unknown;
    }

    protected static CaseStyle DetectStyle(string identifier)
    {
        // camelCase: starts lowercase, followed by one or more capitalized words
        if (Regex.IsMatch(identifier, @"^[a-z]+(?:[A-Z][a-z]*)+$"))
            return CaseStyle.CamelCase;

        // PascalCase: one or more capitalized words, starting with uppercase
        if (Regex.IsMatch(identifier, @"^(?:[A-Z][a-z]*)+$"))
            return CaseStyle.PascalCase;

        // snake_case: lowercase words separated by underscores (at least one underscore)
        if (Regex.IsMatch(identifier, @"^[a-z]+(_[a-z]+)+$"))
            return CaseStyle.SnakeCase;

        // SCREAMING_SNAKE_CASE: uppercase words separated by underscores (at least one underscore)
        if (Regex.IsMatch(identifier, @"^[A-Z]+(_[A-Z]+)+$"))
            return CaseStyle.ScreamingSnakeCase;

        return CaseStyle.Unknown;
    }
}
