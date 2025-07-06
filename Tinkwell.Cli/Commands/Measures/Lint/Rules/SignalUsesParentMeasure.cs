using Tinkwell.Measures.Configuration.Parser;

namespace Tinkwell.Cli.Commands.Measures.Lint.Rules;

[Linter.Rule(category: "best-practice")]
sealed class SignalUsesParentMeasure : Linter.Rule, ITwmLinterRule<SignalDefinition>
{
    public Linter.Issue? Apply(ITwmFile file, object? parent, SignalDefinition item)
    {
        if (parent is null || parent is not MeasureDefinition measure)
            return Ok();

        try
        {
            var dependencies = new NCalc.Expression(item.When).GetParameterNames();
            if (!dependencies.Intersect(["value", measure.Name]).Any())
                return Warning<SignalDefinition>(item.Name, "A signal owned by a measure should reference it.");
        }
        catch
        {
        }

        return Ok();
    }
}
