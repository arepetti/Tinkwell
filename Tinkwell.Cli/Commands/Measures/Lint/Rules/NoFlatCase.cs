using Tinkwell.Measures.Configuration.Parser;

namespace Tinkwell.Cli.Commands.Measures.Lint.Rules;

abstract class NoFlatCase : NamingStyleBase
{
    public Linter.Issue? Validate<T>(ITwmFile file, string identifier)
    {
        var style = DetectStyle(identifier);
        if (style == CaseStyle.Unknown && identifier.Length > 16) // Very very arbitrary
        {
            var dominantStyle = DetectDominantStyle(file);
            if (dominantStyle == CaseStyle.Unknown)
                return Minor<T>(identifier, $"This identifier appears to be flatcase, consider using another option for readability.");
            else
                return Minor<T>(identifier, $"This identifier appears to be flatcase, consider using {dominantStyle} for consistency.");
        }

        return Ok();
    }
}


[Linter.Rule(category: "style-naming")]
sealed class MeasureNoFlatCase : NoFlatCase, ITwmLinterRule<MeasureDefinition>
{
    public Linter.Issue? Apply(ITwmFile file, object? parent, MeasureDefinition item)
        => Validate<MeasureDefinition>(file, item.Name);
}

[Linter.Rule(category: "style-naming")]
sealed class SignalNoFlatCase : NoFlatCase, ITwmLinterRule<SignalDefinition>
{
    public Linter.Issue? Apply(ITwmFile file, object? parent, SignalDefinition item)
        => Validate<SignalDefinition>(file, item.Name);
}
