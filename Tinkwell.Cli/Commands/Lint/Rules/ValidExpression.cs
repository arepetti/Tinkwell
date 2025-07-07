using NCalc.Exceptions;

namespace Tinkwell.Cli.Commands.Lint.Rules;

abstract class ValidExpression : Linter.Rule
{
    protected Linter.Issue? Validate(string target, string name, string expression)
    {
        try
        {
            var expr = new NCalc.Expression(expression);
            expr.EvaluateParameter += (name, args) =>
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
