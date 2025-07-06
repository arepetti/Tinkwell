using Tinkwell.Measures.Configuration.Parser;

namespace Tinkwell.Cli.Commands.Measures.Lint.Rules;

abstract class ConsistentNamingStyle : NamingStyleBase
{
    public Linter.Issue? Validate<T>(IEnumerable<string> identifiers, string identifier)
    {
        var dominantStyle = DetectDominantStyle(identifiers);
        var style = DetectStyle(identifier);
        if (style != CaseStyle.Unknown && style != dominantStyle)
            return Minor<T>(identifier, $"Identifier {identifier} appears to be {style} but the dominant style in the file is {dominantStyle}.");

        return Ok();
    }
}

[Linter.Rule(category: "style-naming")]
sealed class MeasureConsistentNamingStyle : ConsistentNamingStyle, ITwmLinterRule<MeasureDefinition>
{
    public Linter.Issue? Apply(ITwmFile file, object? parent, MeasureDefinition item)
        => Validate< MeasureDefinition>(CollectAllIdentifiers(file), item.Name);
}

[Linter.Rule(category: "style-naming")]
sealed class SignalConsistentNamingStyle : ConsistentNamingStyle, ITwmLinterRule<SignalDefinition>
{
    public Linter.Issue? Apply(ITwmFile file, object? parent, SignalDefinition item)
        => Validate<SignalDefinition>(CollectAllIdentifiers(file), item.Name);
}
