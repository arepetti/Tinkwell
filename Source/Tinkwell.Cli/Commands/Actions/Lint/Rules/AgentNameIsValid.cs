using Tinkwell.Actions.Configuration.Parser;
using Tinkwell.Actions.Executor;
using Tinkwell.Cli.Commands.Lint;
using Tinkwell.Cli.Commands.Lint.Rules;

namespace Tinkwell.Cli.Commands.Ensamble.Lint.Rules;

sealed class AgentNameIsValid : ValidExpression, ITwaLinterRule<WhenDefinition>
{
    public IEnumerable<Linter.Issue?> Apply(ITwaFile file, object? parent, WhenDefinition item)
    {
        List<Linter.Issue?> issues = new();
        foreach (var action in item.Actions)
        {
            if (!AgentsRecruiter.Exists(action.Name))
                issues.Add(Critical<WhenDefinition>(item.Name ?? item.Topic, $"Agent '{action.Name}' does not exist."));
        }

        return issues;
    }
}
