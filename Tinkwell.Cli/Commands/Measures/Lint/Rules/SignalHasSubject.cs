using Tinkwell.Measures.Configuration.Parser;

namespace Tinkwell.Cli.Commands.Measures.Lint.Rules;

sealed class SignalHasSubject : Linter.Rule, ITwmLinterRule<SignalDefinition>
{
    public Linter.Issue? Apply(ITwmFile file, object? parent, SignalDefinition item)
    {
        if (parent is not null || item.Payload.ContainsKey("subject"))
            return Ok();

        return Minor<SignalDefinition>(item.Name, "A signal not owned by a measure should specify a subject.");
    }
}
