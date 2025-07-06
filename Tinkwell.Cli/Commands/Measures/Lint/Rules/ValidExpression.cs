using NCalc.Exceptions;
using NCalc.Handlers;
using Tinkwell.Measures.Configuration.Parser;

namespace Tinkwell.Cli.Commands.Measures.Lint.Rules;

abstract class ValidExpression : Linter.Rule
{
    protected Linter.Issue? Validate(string target, string name, string expression)
    {
        try
        {
            var expr = new NCalc.Expression(expression);
            expr.EvaluateParameter += (string name, ParameterArgs args) =>
            {
                args.Result = 1;
            };
            expr.Evaluate();
        }
        catch (NCalcParserException e)
        {
            string message = e.GetBaseException()?.Message ?? e.Message;
            return new Linter.Issue(Id, Linter.IssueSeverity.Critical, target, name, message);
        }
        catch
        {
        }

        return Ok();
    }
}

sealed class MeasureValidExpression : ValidExpression, ITwmLinterRule<MeasureDefinition>
{
    public Linter.Issue? Apply(ITwmFile file, object? parent, MeasureDefinition item)
    {
        if (string.IsNullOrWhiteSpace(item.Expression))
            return Ok();

        return Validate(nameof(MeasureDefinition), item.Name, item.Expression);
    }
}

sealed class SignalValidExpression : ValidExpression, ITwmLinterRule<SignalDefinition>
{
    public Linter.Issue? Apply(ITwmFile file, object? parent, SignalDefinition item)
        => Validate(nameof(SignalDefinition), item.Name, item.When);
}
