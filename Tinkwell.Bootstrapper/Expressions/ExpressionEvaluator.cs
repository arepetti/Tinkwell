using NCalc;
using NCalc.Exceptions;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;

namespace Tinkwell.Bootstrapper.Expressions;

/// <summary>
/// Evaluates expressions using the NCalc engine and supports custom functions and parameter import.
/// </summary>
public sealed class ExpressionEvaluator : IExpressionEvaluator
{
    internal static readonly object Undefined = new object();

    /// <summary>
    /// Evaluates the specified expression with optional parameters.
    /// </summary>
    /// <param name="expression">The expression to evaluate.</param>
    /// <param name="parameters">Optional parameters for the expression.</param>
    /// <returns>The result of the evaluation.</returns>
    /// <exception cref="BootstrapperException">Thrown if evaluation fails.</exception>
    public object? Evaluate(string expression, object? parameters)
    {
        var expr = new Expression(expression);
        expr.EvaluateFunction += OnEvaluateFunction;
        expr.EvaluateParameter += (name, args) => OnEvaluateParameter(expr, name, args);
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

    /// <summary>
    /// Evaluates the specified expression and returns the result as a string.
    /// </summary>
    /// <param name="expression">The expression to evaluate.</param>
    /// <param name="parameters">Optional parameters for the expression.</param>
    /// <returns>The result as a string.</returns>
    public string EvaluateString(string expression, object? parameters)
        => Convert.ToString(Evaluate(expression, parameters), CultureInfo.InvariantCulture) ?? "";

    /// <summary>
    /// Evaluates the specified expression and returns the result as a boolean.
    /// </summary>
    /// <param name="expression">The expression to evaluate.</param>
    /// <param name="parameters">Optional parameters for the expression.</param>
    /// <returns>The result as a boolean.</returns>
    /// <exception cref="BootstrapperException">Thrown if casting fails.</exception>
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

    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
    private readonly static IEnumerable<ICustomFunction> _customFunctions =
        StrategyAssemblyLoader.FindTypesImplementing<ICustomFunction>(typeof(ExpressionEvaluator).Assembly)
            .Select(x => (ICustomFunction)Activator.CreateInstance(x)!);

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

    private void OnEvaluateFunction(string name, NCalc.Handlers.FunctionArgs args)
    {
        var function = _customFunctions
            .FirstOrDefault(x => string.Equals(name, x.Name, StringComparison.Ordinal));

        if (function is null)
            return;

        var result = function.Call(args);
        if (ReferenceEquals(result, Undefined))
            return;

        args.Result = result;
    }

    private void OnEvaluateParameter(Expression expr, string name, NCalc.Handlers.ParameterArgs args)
    {
        if (args.Result is not null)
            return;

        var parts = name.Split('.');
        if (parts.Length <= 1)
            return;

        object? currentObject;
        if (expr.Parameters.TryGetValue(parts[0], out var rootObject))
            currentObject = rootObject;
        else
            return;

        for (int i = 1; i < parts.Length; i++)
        {
            if (currentObject is null)
                return;

            var propertyInfo = currentObject.GetType().GetProperty(parts[i], BindingFlags.Public | BindingFlags.Instance);
            if (propertyInfo is null)
                return;

            currentObject = propertyInfo.GetValue(currentObject);
        }

        args.Result = currentObject;
    }
}
