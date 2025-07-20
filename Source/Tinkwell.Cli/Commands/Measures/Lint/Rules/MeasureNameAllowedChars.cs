using Tinkwell.Cli.Commands.Lint;
using Tinkwell.Cli.Commands.Lint.Rules;
using Tinkwell.Measures.Configuration.Parser;

namespace Tinkwell.Cli.Commands.Measures.Lint.Rules;

sealed class MeasureNameAllowedChars : NameAllowedCharsLinerRuleBase, ITwmLinterRule<MeasureDefinition>
{
    public Linter.Issue? Apply(ITwmFile file, object? parent, MeasureDefinition item)
        => Validate(nameof(MeasureDefinition), item.Name);
}
