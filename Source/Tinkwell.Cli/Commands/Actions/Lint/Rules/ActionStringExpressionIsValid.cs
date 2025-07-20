using Tinkwell.Actions.Configuration.Parser;
using Tinkwell.Cli.Commands.Lint;
using Tinkwell.Cli.Commands.Lint.Rules;

namespace Tinkwell.Cli.Commands.Ensamble.Lint.Rules;

sealed class ActionStringExpressionIsValid : ValidExpression, ITwaLinterRule<WhenDefinition>
{
    public IEnumerable<Linter.Issue?> Apply(ITwaFile file, object? parent, WhenDefinition item)
    {
        List<Linter.Issue?> issues = new();
        foreach (var action in item.Actions)
        {
            foreach (var entry in action.Properties)
            {
                if (entry.Value is ActionPropertyString str && str.Kind == ActionPropertyStringKind.Expression)
                {
                    var issue = Validate(nameof(WhenDefinition), item.Name ?? item.Topic, str.Value);
                    if (issue is not null)
                        issues.Add(issue);
                }
            }
        }    

        return issues;
    }
}
