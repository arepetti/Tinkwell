using Tinkwell.Measures.Configuration.Parser;

namespace Tinkwell.Cli.Commands.Measures.Lint.Rules;

abstract class NameAllowedChars : Linter.Rule
{
    protected Linter.Issue? Validate(string target, string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return new Linter.Issue(Id, Linter.IssueSeverity.Error, target, name, $"Name cannot be empty");

        if (InvalidPrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal)))
            return new Linter.Issue(Id, Linter.IssueSeverity.Error, target, name, $"Name cannot start with: {string.Join("", InvalidPrefixes)}");

        if (InvalidCharacters.Any(c => name.Contains(c, StringComparison.Ordinal)))
            return new Linter.Issue(Id, Linter.IssueSeverity.Error, target, name, $"Name cannot contain any of: {string.Join("", InvalidCharacters)}");

        return Ok();
    }

    private static readonly string[] InvalidPrefixes = ["+", "-", "/", "__"];
    private static readonly string[] InvalidCharacters = ["[", "]", "{", "}", "\\", "*", ":", ";", "\"", "'", "=", "!", "?"];
}


sealed class MeasureNameAllowedChars : NameAllowedChars, ITwmLinterRule<MeasureDefinition>
{
    public Linter.Issue? Apply(ITwmFile file, object? parent, MeasureDefinition item)
        => Validate(nameof(MeasureDefinition), item.Name);
}

sealed class SignalNameAllowedChars : NameAllowedChars, ITwmLinterRule<SignalDefinition>
{
    public Linter.Issue? Apply(ITwmFile file, object? parent, SignalDefinition item)
        => Validate(nameof(SignalDefinition), item.Name);
}
