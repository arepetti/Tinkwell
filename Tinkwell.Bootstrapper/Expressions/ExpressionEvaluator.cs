using NCalc;
using NCalc.Exceptions;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;

namespace Tinkwell.Bootstrapper.Expressions;

public sealed class ExpressionEvaluator : IExpressionEvaluator
{
    internal static readonly object Null = new object();

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

    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
    private readonly static IEnumerable<INCalcCustomFunction> _customFunctions =
        Assembly.GetExecutingAssembly()
            .GetTypes()
            .Where(x => x.IsClass && !x.IsAbstract && typeof(INCalcCustomFunction).IsAssignableFrom(x))
            .Select(x => (INCalcCustomFunction)Activator.CreateInstance(x)!);

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
        if (ReferenceEquals(result, Null))
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
