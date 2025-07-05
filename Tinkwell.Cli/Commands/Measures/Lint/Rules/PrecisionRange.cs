using Tinkwell.Measures.Configuration.Parser;

namespace Tinkwell.Cli.Commands.Measures.Lint.Rules;

sealed class PrecisionRange : Linter.Rule, ITwmLinterRule<MeasureDefinition>
{
    public Linter.Issue? Apply(ITwmFile file, object? parent, MeasureDefinition item)
    {
        if (item.Precision.HasValue && item.Precision.Value < 0)
            return Critical<MeasureDefinition>(item.Name, "Precision should be between 0 and 17.");

        if (item.Precision.HasValue && item.Precision.Value > 17)
            return Warning<MeasureDefinition>(item.Name, "Precision should be between 0 and 17.");

        return Ok();
    }
}
