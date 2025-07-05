using Tinkwell.Measures.Configuration.Parser;

namespace Tinkwell.Cli.Commands.Measures.Lint.Rules;

sealed class NameAllowedChars : Linter.Rule, ITwmLinterRule<MeasureDefinition>, ITwmLinterRule<SignalDefinition>
{
    public Linter.Issue? Apply(ITwmFile file, object? parent, MeasureDefinition item)
        => Validate(nameof(MeasureDefinition), item.Name);

    public Linter.Issue? Apply(ITwmFile file, object? parent, SignalDefinition item)
        => Validate(nameof(SignalDefinition), item.Name);

    private static readonly string[] InvalidPrefixes = ["+", "-", "/", "__"];
    private static readonly string[] InvalidCharacters = ["[", "]", "{", "}", "\\", "*", ":", ";", "\"", "'", "=", "!", "?"];

    private Linter.Issue? Validate(string target, string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return new Linter.Issue(Id, Linter.IssueSeverity.Error, target, name, $"Name cannot be empty");

        if (InvalidPrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal)))
            return new Linter.Issue(Id, Linter.IssueSeverity.Error, target, name, $"Name cannot start with: {string.Join("", InvalidPrefixes)}");

        if (InvalidCharacters.Any(c => name.Contains(c, StringComparison.Ordinal)))
            return new Linter.Issue(Id, Linter.IssueSeverity.Error, target, name, $"Name cannot contain any of: {string.Join("", InvalidCharacters)}");

        return Ok();
    }
}
