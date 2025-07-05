using Tinkwell.Measures.Configuration.Parser;

namespace Tinkwell.Cli.Commands.Measures.Lint.Rules;

sealed class MeasureNameIsValid : Linter.Rule, ITwmLinterRule<MeasureDefinition>
{
    public Linter.Issue? Apply(ITwmFile file, object? parent, MeasureDefinition item)
    {
        if (NamesWithMinorIssue.Contains(item.Name, StringComparer.Ordinal))
            return Minor<MeasureDefinition>(item.Name, $"This name should not be useed for a measure.");

        if (NamesWithWarning.Contains(item.Name, StringComparer.Ordinal))
            return Warning<MeasureDefinition>(item.Name, $"This name should not be useed for a measure.");

        return null;

    }

    private static readonly string[] NamesWithMinorIssue = ["let", "when", "then", "emit", "measure", "signal", "import"];
    private static readonly string[] NamesWithWarning = ["value"];
}
