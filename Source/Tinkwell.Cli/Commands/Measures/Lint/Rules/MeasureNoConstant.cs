using Tinkwell.Cli.Commands.Lint;
using Tinkwell.Measures.Configuration.Parser;

namespace Tinkwell.Cli.Commands.Measures.Lint.Rules;

[Linter.Rule(strict: true, category: "best-practice")]
sealed class MeasureNoConstant: Linter.Rule, ITwmLinterRule<MeasureDefinition>
{
    public Linter.Issue? Apply(ITwmFile file, object? parent, MeasureDefinition item)
    {
        try
        {
            var dependencies = new NCalc.Expression(item.Expression).GetParameterNames();
            if (!dependencies.Any())
                return Minor<MeasureDefinition>(item.Name, "Constant measures should be avoided.");
        }
        catch
        {
        }

        return Ok();
    }
}
