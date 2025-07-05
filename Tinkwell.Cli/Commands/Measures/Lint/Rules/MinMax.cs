using Tinkwell.Measures.Configuration.Parser;

namespace Tinkwell.Cli.Commands.Measures.Lint.Rules;

sealed class MinMax : Linter.Rule, ITwmLinterRule<MeasureDefinition>
{
    public Linter.Issue? Apply(ITwmFile file, object? parent, MeasureDefinition item)
    {
        if (item.Minimum.HasValue && item.Maximum.HasValue && item.Minimum.Value >= item.Maximum.Value)
            return new Linter.Issue(Id, Linter.IssueSeverity.Critical, nameof(MeasureDefinition), item.Name, "Minimum cannot be higher (or equal) than maximum.");

        return null;
    }
}
