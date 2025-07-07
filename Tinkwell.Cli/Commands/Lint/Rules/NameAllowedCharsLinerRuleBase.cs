namespace Tinkwell.Cli.Commands.Lint.Rules;

abstract class NameAllowedCharsLinerRuleBase : Linter.Rule
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
