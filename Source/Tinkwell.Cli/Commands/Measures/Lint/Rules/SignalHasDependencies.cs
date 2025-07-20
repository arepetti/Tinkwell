using Tinkwell.Cli.Commands.Lint;
using Tinkwell.Measures.Configuration.Parser;

namespace Tinkwell.Cli.Commands.Measures.Lint.Rules;

sealed class SignalHasDependencies : Linter.Rule, ITwmLinterRule<SignalDefinition>
{
    public Linter.Issue? Apply(ITwmFile file, object? parent, SignalDefinition item)
    {
        try
        {
            var dependencies = new NCalc.Expression(item.When).GetParameterNames();
            if (!dependencies.Any())
                return Warning<SignalDefinition>(item.Name, "A signal should have dependencies.");
        }
        catch
        {
        }

        return Ok();
    }
}
