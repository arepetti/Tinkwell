using NCalc;
using NCalc.Exceptions;
using System.Globalization;

namespace Tinkwell.Bootstrapper.Expressions;

public sealed class ExpressionEvaluator : IExpressionEvaluator
{
    public object? Evaluate(string expression, object? parameters)
    {
        var expr = new Expression(expression);
        expr.CultureInfo = CultureInfo.InvariantCulture;

        ImportParameters(parameters, expr);

        try
        {
            return expr.Evaluate();
        }
        catch (NCalcException e)
        {
            throw new BootstrapperException($"Error evaluating an expression: {e.Message}", e);
        }
    }

    public string EvaluateString(string expression, object? parameters)
        => Convert.ToString(Evaluate(expression, parameters), CultureInfo.InvariantCulture) ?? "";

    public bool EvaluateBool(string expression, object? parameters)
    {
        try
        {
            return Convert.ToBoolean(Evaluate(expression, parameters), CultureInfo.InvariantCulture);
        }
        catch (FormatException e)
        {
            throw new BootstrapperException($"Error casting the result of an expression: {e.Message}", e);
        }
        catch (InvalidCastException e)
        {
            throw new BootstrapperException($"Error casting the result of an expression: {e.Message}", e);
        }
    }

    private static void ImportParameters(object? parameters, Expression expr)
    {
        if (parameters is not null)
        {
            if (parameters is System.Collections.IDictionary dictionary)
                ImportParametersFromDictionary("", dictionary, expr);
            else
                ImportParametersFromObject(parameters, expr);
        }
    }

    private static void ImportParametersFromDictionary(string prefix, System.Collections.IDictionary parameters, Expression expr)
    {
        foreach (System.Collections.DictionaryEntry kvp in parameters)
            expr.Parameters[prefix + Convert.ToString(kvp.Key, CultureInfo.InvariantCulture)!] = kvp.Value;
    }

    private static void ImportParametersFromObject(object parameters, Expression expr)
    {
        foreach (var property in parameters.GetType().GetProperties())
        {
            var value = property.GetValue(parameters);
            if (value is not null)
            {
                if (value is System.Collections.IDictionary dictionary)
                    ImportParametersFromDictionary($"{property.Name}.", dictionary, expr);
                else
                    expr.Parameters[property.Name] = value;
            }
        }
    }
}
