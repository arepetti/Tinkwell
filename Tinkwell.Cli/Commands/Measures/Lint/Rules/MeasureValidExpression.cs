using Tinkwell.Cli.Commands.Lint;
using Tinkwell.Cli.Commands.Lint.Rules;
using Tinkwell.Measures.Configuration.Parser;

namespace Tinkwell.Cli.Commands.Measures.Lint.Rules;

sealed class MeasureValidExpression : ValidExpression, ITwmLinterRule<MeasureDefinition>
{
    public Linter.Issue? Apply(ITwmFile file, object? parent, MeasureDefinition item)
    {
        if (string.IsNullOrWhiteSpace(item.Expression))
            return Ok();

        return Validate(nameof(MeasureDefinition), item.Name, item.Expression);
    }
}
