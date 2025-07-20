using NCalc;
using NCalc.Exceptions;
using NCalc.Handlers;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;

namespace Tinkwell.Bootstrapper.Expressions;

/// <summary>
/// Evaluates expressions using the NCalc engine and supports custom functions and parameter import.
/// </summary>
public sealed class ExpressionEvaluator : IExpressionEvaluator
{
    /// <summary>
    /// Represents an <em>undefined</em> object. When present in parameters it'll
    /// always be ignored.
    /// </summary>
    public static readonly object Undefined = new object();

    /// <summary>
    /// Evaluates the specified expression with optional parameters.
    /// </summary>
    /// <param name="expression">The expression to evaluate.</param>
    /// <param name="parameters">
    /// Optional parameters for the expression. If this is an object then each property
    /// becomes a parameter (unless its value is <see cref="Undefined"/>). If a property contains
    /// a dictionary (or <paramref name="parameters"/> itself is a dictionary) then all its entries become
    /// parameters (accessible using dot-notation like <c>dictionary.entry_key</c>, if their value is not <c>Undefined</c>).
    /// Nested objects or nested dictionaries are not supported.
    /// </param>
    /// <returns>The result of the evaluation.</returns>
    /// <exception cref="BootstrapperException">If the expression is not valid.</exception>
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
        catch (NCalcFunctionNotFoundException e)
        {
            throw new BootstrapperException($"Function {e.FunctionName}() does not exist.");
        }
        catch (NCalcParameterNotDefinedException e)
        {
            throw new BootstrapperException($"Parameter '{e.ParameterName}' is not defined.");
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
    /// <param name="parameters">
    /// Optional parameters for the expression. If this is an object then each property
    /// becomes a parameter (unless its value is <see cref="Undefined"/>). If a property contains
    /// a dictionary (or <paramref name="parameters"/> itself is a dictionary) then all its entries become
    /// parameters (accessible using dot-notation like <c>dictionary.entry_key</c>, if their value is not <c>Undefined</c>).
    /// Nested objects or nested dictionaries are not supported.
    /// </param>
    /// <returns>
    /// The result as a string. Conversions are performed using the invariant culture. A <c>null</c>
    /// value returns an empty string.
    /// </returns>
    /// <exception cref="BootstrapperException">If the expression is not valid.</exception>
    public string EvaluateString(string expression, object? parameters)
        => Convert.ToString(Evaluate(expression, parameters), CultureInfo.InvariantCulture) ?? "";

    /// <summary>
    /// Evaluates the specified expression and returns the result as a boolean.
    /// </summary>
    /// <param name="expression">The expression to evaluate.</param>
    /// <param name="parameters">
    /// Optional parameters for the expression. If this is an object then each property
    /// becomes a parameter (unless its value is <see cref="Undefined"/>). If a property contains
    /// a dictionary (or <paramref name="parameters"/> itself is a dictionary) then all its entries become
    /// parameters (accessible using dot-notation like <c>dictionary.entry_key</c>, if their value is not <c>Undefined</c>).
    /// Nested objects or nested dictionaries are not supported.
    /// </param>
    /// <returns>The result as a boolean.</returns>
    /// <exception cref="BootstrapperException">
    /// <para>
    /// If the expression is not valid.
    /// </para>
    /// <para>-or-</para>
    /// <para>
    /// If the value cannot be converted to <c>boolean</c>.
    /// </para>
    /// </exception>
    public bool EvaluateBool(string expression, object? parameters)
        => EvaluateTo(expression, parameters, value => Convert.ToBoolean(value, CultureInfo.InvariantCulture));

    /// <summary>
    /// Evaluates the specified expression and returns the result as a <c>double</c>.
    /// </summary>
    /// <param name="expression">The expression to evaluate.</param>
    /// <param name="parameters">
    /// Optional parameters for the expression. If this is an object then each property
    /// becomes a parameter (unless its value is <see cref="Undefined"/>). If a property contains
    /// a dictionary (or <paramref name="parameters"/> itself is a dictionary) then all its entries become
    /// parameters (accessible using dot-notation like <c>dictionary.entry_key</c>, if their value is not <c>Undefined</c>).
    /// Nested objects or nested dictionaries are not supported.
    /// </param>
    /// <returns>
    /// The result as a double. Note that the conversion is fairly aggressive when trying to obtain the
    /// desired type. Parsing is always performed using the invariant culture.
    /// </returns>
    /// <exception cref="BootstrapperException">
    /// <para>
    /// If the expression is not valid.
    /// </para>
    /// <para>-or-</para>
    /// <para>
    /// If the value cannot be converted to <c>double</c>.
    /// </para>
    /// </exception>
    public double EvaluateDoble(string expression, object? parameters)
        => EvaluateTo(expression, parameters, value => Convert.ToDouble(value, CultureInfo.InvariantCulture));

    /// <summary>
    /// Evaluates the specified expression and returns the result as a <c>bool</c>.
    /// </summary>
    /// <param name="expression">The expression to evaluate.</param>
    /// <param name="parameters">
    /// Optional parameters for the expression. If this is an object then each property
    /// becomes a parameter (unless its value is <see cref="Undefined"/>). If a property contains
    /// a dictionary (or <paramref name="parameters"/> itself is a dictionary) then all its entries become
    /// parameters (accessible using dot-notation like <c>dictionary.entry_key</c>, if their value is not <c>Undefined</c>).
    /// Nested objects or nested dictionaries are not supported.
    /// </param>
    /// <param name="result">
    /// The result as a boolean. Parsing is always performed using the invariant culture.
    /// </param>
    /// <returns>
    /// <c>true</c> if the evaluation was successful, <c>false</c> otherwise. Note that
    /// <strong>errors in the expression always causes an exception</strong>, the return value
    /// might be <c>false</c> only when the conversion from the expression's return value
    /// to the destination type fails.
    /// </returns>
    /// <exception cref="BootstrapperException"> If the expression is not valid.</exception>
    public bool TryEvaluateBool(string expression, object? parameters, out bool result)
        => TryEvaluateTo(expression, parameters, value => Convert.ToBoolean(value, CultureInfo.InvariantCulture), out result);

    /// <summary>
    /// Evaluates the specified expression and returns the result as a <c>double</c>.
    /// </summary>
    /// <param name="expression">The expression to evaluate.</param>
    /// <param name="parameters">
    /// Optional parameters for the expression. If this is an object then each property
    /// becomes a parameter (unless its value is <see cref="Undefined"/>). If a property contains
    /// a dictionary (or <paramref name="parameters"/> itself is a dictionary) then all its entries become
    /// parameters (accessible using dot-notation like <c>dictionary.entry_key</c>, if their value is not <c>Undefined</c>).
    /// Nested objects or nested dictionaries are not supported.
    /// </param>
    /// <param name="result">
    /// The result as a double. Note that the conversion is fairly aggressive when trying to obtain the
    /// desired type. Parsing is always performed using the invariant culture.
    /// </param>
    /// <returns>
    /// <c>true</c> if the evaluation was successful, <c>false</c> otherwise. Note that
    /// <strong>errors in the expression always causes an exception</strong>, the return value
    /// might be <c>false</c> only when the conversion from the expression's return value
    /// to the destination type fails.
    /// </returns>
    /// <exception cref="BootstrapperException"> If the expression is not valid.</exception>
    public bool TryEvaluateDouble(string expression, object? parameters, out double result)
        => TryEvaluateTo(expression, parameters, value => Convert.ToDouble(value, CultureInfo.InvariantCulture), out result);

    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
    private readonly static IDictionary<string, ICustomFunction> _customFunctions =
        StrategyAssemblyLoader.FindTypesImplementing<ICustomFunction>(typeof(ExpressionEvaluator).Assembly)
            .Select(x => (ICustomFunction)Activator.CreateInstance(x)!)
            .ToDictionary(x => x.Name, x => x);

    private T EvaluateTo<T>(string expression, object? parameters, Func<object?, T> convert)
    {
        object? value = Evaluate(expression, parameters);
        if (value is null)
            throw new BootstrapperException($"Cannot convert a null value to '{typeof(T).Name}'.");

        try
        {
            return convert(value);
        }
        catch (FormatException e)
        {
            throw new BootstrapperException($"Cannot convert '{value}' ('{value?.GetType().Name ?? "n/a"}') to '{typeof(T).Name}': {e.Message}", e);
        }
        catch (InvalidCastException e)
        {
            throw new BootstrapperException($"Cannot convert '{value}' ('{value?.GetType().Name ?? "n/a"}') to '{typeof(T).Name}': {e.Message}", e);
        }
    }

    private bool TryEvaluateTo<T>(string expression, object? parameters, Func<object?, T> convert, out T result)
    {
        result = default!;

        try
        {
            object? value = Evaluate(expression, parameters);
            if (value is null)
                return false;

            result = convert(value);
            return true;
        }
        catch (Exception e) when (e is not BootstrapperException)
        {
            return false;
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
        {
            if (ReferenceEquals(kvp.Value, Undefined))
                continue;

            expr.Parameters[prefix + Convert.ToString(kvp.Key, CultureInfo.InvariantCulture)!] = kvp.Value;
        }
    }

    private static void ImportParametersFromObject(object parameters, Expression expr)
    {
        foreach (var property in parameters.GetType().GetProperties())
        {
            var value = property.GetValue(parameters);
            if (ReferenceEquals(value, Undefined))
                continue;

            if (value is System.Collections.IDictionary dictionary)
                ImportParametersFromDictionary($"{property.Name}.", dictionary, expr);
            else
                expr.Parameters[property.Name] = value;
        }
    }

    private void OnEvaluateParameter(Expression expr, string name, ParameterArgs args)
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
            // Skipping null objects what we're implementing is basically the null-conditional operator
            if (currentObject is null || ReferenceEquals(currentObject, Undefined))
                return;

            var propertyInfo = currentObject.GetType().GetProperty(parts[i], BindingFlags.Public | BindingFlags.Instance);
            if (propertyInfo is null)
                return;

            currentObject = propertyInfo.GetValue(currentObject);
        }

        args.Result = currentObject;
    }

    private void OnEvaluateFunction(string name, FunctionArgs args)
    {
        if (!_customFunctions.TryGetValue(name, out var function))
            return;

        var result = function.Call(args);
        if (ReferenceEquals(result, Undefined))
            return;

        args.Result = result;
    }
}
