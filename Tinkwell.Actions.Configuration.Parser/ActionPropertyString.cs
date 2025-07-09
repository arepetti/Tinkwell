using Tinkwell.Bootstrapper.Expressions;

namespace Tinkwell.Actions.Configuration.Parser;

public enum ActionPropertyStringKind
{
    Plain,
    Expression,
    Template
}

public sealed record ActionPropertyString(ActionPropertyStringKind Kind, string Value)
{
    public string ToString(object? data)
    {
        return Kind switch
        {
            ActionPropertyStringKind.Plain => Value,
            ActionPropertyStringKind.Expression => EvaluateExpression(Value, data),
            ActionPropertyStringKind.Template => EvaluateTemplate(Value, data),
            _ => throw new NotSupportedException("Unknown ActionPropertyStringKind")
        };
    }

    private string EvaluateTemplate(string value, object? data)
    {
        return new TemplateRenderer().Render(value, data);
    }

    private string EvaluateExpression(string value, object? data)
        => new ExpressionEvaluator().EvaluateString(value, data);
}