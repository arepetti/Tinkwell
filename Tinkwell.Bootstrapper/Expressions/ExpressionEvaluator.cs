using NCalc;
using NCalc.Exceptions;
using System.Globalization;

namespace Tinkwell.Bootstrapper.Expressions;

public sealed class ExpressionEvaluator : IExpressionEvaluator
{
    public bool EvaluateBool(string expression, object? parameters)
    {
        var expr = new Expression(expression);
        expr.CultureInfo = CultureInfo.InvariantCulture;

        if (parameters is not null)
        {
            if (parameters is System.Collections.IDictionary dictionary)
                ImportParametersFromDictionary(dictionary, expr);
            else
                ImportParametersFromObject(parameters, expr);
        }

        try
        {
            return Convert.ToBoolean(expr.Evaluate(), CultureInfo.InvariantCulture);
        }
        catch (NCalcException e)
        {
            throw new BootstrapperException($"Error evaluating an expression: {e.Message}", e);
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

    private static void ImportParametersFromDictionary(System.Collections.IDictionary parameters, Expression expr)
    {
        foreach (System.Collections.DictionaryEntry kvp in parameters)
            expr.Parameters[Convert.ToString(kvp.Key, CultureInfo.InvariantCulture)!] = kvp.Value;
    }

    private static void ImportParametersFromObject(object parameters, Expression expr)
    {
        foreach (var property in parameters.GetType().GetProperties())
        {
            var value = property.GetValue(parameters);
            if (value is not null)
                expr.Parameters[property.Name] = value;
        }
    }
}