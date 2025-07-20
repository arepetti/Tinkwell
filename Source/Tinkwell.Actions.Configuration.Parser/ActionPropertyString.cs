using Tinkwell.Bootstrapper.Expressions;

namespace Tinkwell.Actions.Configuration.Parser;

/// <summary>
/// Represent the type of string an <c>ActionPropertyString</c> contains.
/// </summary>
public enum ActionPropertyStringKind
{
    /// <summary>
    /// Plain string, to be used as-is.
    /// </summary>
    Plain,

    /// <summary>
    /// The string is an expression that needs to be evaluated to produce the string to use.
    /// </summary>
    Expression,

    /// <summary>
    /// The string is a Liquid template that needs to be rendered with the provided data
    /// to produce the string to use.
    /// </summary>
    Template
}

/// <summary>
/// Represents a string in the properties of an <c>ActionDefinition</c>.
/// </summary>
/// <param name="Kind">Kind of string.</param>
/// <param name="Value">The raw value of the string.</param>
public sealed record ActionPropertyString(ActionPropertyStringKind Kind, string Value)
{
    /// <summary>
    /// Converts the raw string <see cref="Value"/> into the final usable string,
    /// transformed in the way specified by <see cref="Kind"/>.
    /// </summary>
    /// <param name="data">Data used to transform the string.</param>
    /// <returns>The string in its final usable form.</returns>
    public string ToString(object? data)
    {
        return Kind switch
        {
            ActionPropertyStringKind.Expression => EvaluateExpression(Value, data),
            ActionPropertyStringKind.Template => EvaluateTemplate(Value, data),
            _ => Value,
        };
    }

    private string EvaluateTemplate(string value, object? data)
    {
        return new TemplateRenderer().Render(value, data);
    }

    private string EvaluateExpression(string value, object? data)
        => new ExpressionEvaluator().EvaluateString(value, data);
}