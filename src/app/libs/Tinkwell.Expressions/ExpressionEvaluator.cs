using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using NCalc;
using Tinkwell.Expressions.Functions;
using Tinkwell.Telemetry;

namespace Tinkwell.Expressions;

/// <summary>
/// Default <see cref="IExpressionEvaluator"/> implementation backed by NCalc.
/// </summary>
/// <remarks>
/// <para>
/// Expressions are parsed and evaluated using <see cref="NCalc.Expression"/>.
/// Parameter names in the <c>parameters</c> dictionary must <strong>match
/// the casing</strong> used in the expression (e.g. <c>[myParam] > 10</c> or
/// <c>myParam > 10</c> with a <c>myParam</c> key).
/// <see cref="NCalc.ExpressionOptions.IgnoreCaseAtBuiltInFunctions"/>, as
/// configured on the internal <see cref="NCalc.Expression"/>, makes NCalc
/// <em>built-in</em> function names (e.g. <c>Abs</c> / <c>abs</c>) case-insensitive; it
/// does <strong>not</strong> make your <c>[parameter]</c> names
/// case-insensitive. Tinkwell custom <see cref="IExpressionFunction"/> names
/// are looked up with ordinal, case-sensitive matching.
/// </para>
/// <para>
/// Custom functions registered via <see cref="IExpressionFunction"/> are
/// dispatched automatically. Use <see cref="ExpressionFunctionDiscovery"/>
/// to discover functions from assemblies, or pass them explicitly.
/// </para>
/// <para>
/// A single <see cref="ExpressionEvaluator"/> instance is safe to use from
/// multiple threads for concurrent evaluations. Each call uses its own
/// NCalc expression object and, when a parameter dictionary is supplied,
/// NCalc is populated from that copy — there is no shared per-evaluator
/// parameter state. Do not share mutable object graphs across concurrent
/// evaluations unless the types are thread-safe.
/// </para>
/// <para>
/// When <see cref="ExpressionEvaluationOptions"/> applies a non-infinite
/// <see cref="ExpressionEvaluationOptions.Timeout"/>, only the time spent
/// waiting for the result is limited; the underlying evaluation may
/// still run to completion on a thread-pool thread after the timeout. See
/// the package README and <see cref="ExpressionEvaluationOptions"/> for details.
/// </para>
/// </remarks>
public sealed class ExpressionEvaluator : IExpressionEvaluator
{
    private readonly Dictionary<string, IExpressionFunction> _functions;

    /// <summary>
    /// Creates an evaluator with the specified function set.
    /// </summary>
    /// <param name="functions">
    /// The function set to register. Passing <see langword="null"/> (or an
    /// empty collection) yields an evaluator with no Tinkwell built-ins —
    /// only NCalc's own functions remain available. Passing a non-empty
    /// list <strong>replaces</strong> the built-ins; combine with
    /// <see cref="ExpressionFunctionDiscovery.BuiltIn"/> if you want both,
    /// e.g. <c>ExpressionFunctionDiscovery.BuiltIn().Concat(myFunctions)</c>.
    /// This overload is also the trim- and AOT-safe entry point: prefer it
    /// in <c>PublishTrimmed</c> / <c>PublishAot</c> apps so reflection-based
    /// discovery is avoided.
    /// </param>
    public ExpressionEvaluator(IEnumerable<IExpressionFunction>? functions)
    {
        _functions = (functions ?? [])
            .ToDictionary(f => f.Name, StringComparer.Ordinal);
    }

    /// <summary>
    /// Creates an evaluator with all Tinkwell built-in functions enabled.
    /// Recommended default. Built-ins are discovered from this assembly
    /// via reflection on first use.
    /// </summary>
    /// <remarks>
    /// For trim- or AOT-published apps, use
    /// <see cref="ExpressionEvaluator(IEnumerable{IExpressionFunction}?)"/>
    /// with an explicit list so the linker preserves the function types.
    /// </remarks>
    [RequiresUnreferencedCode("Discovers built-in functions via reflection. Use the overload accepting IEnumerable<IExpressionFunction> for trim/AOT compatibility.")]
    public ExpressionEvaluator() : this(ExpressionFunctionDiscovery.BuiltIn())
    {
    }

    /// <inheritdoc/>
    public Task<object?> EvaluateAsync(
        string expression,
        IReadOnlyDictionary<string, object?>? parameters = null,
        ExpressionEvaluationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(expression);
        return RunWithTimeoutAsync(() => EvaluateCore(expression, parameters), expression, options, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<bool> EvaluateBooleanAsync(
        string expression,
        IReadOnlyDictionary<string, object?>? parameters = null,
        ExpressionEvaluationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(expression);
        var result = await RunWithTimeoutAsync(
            () => EvaluateCore(expression, parameters), expression, options, cancellationToken);
        return CoerceToBoolean(result, expression);
    }

    /// <inheritdoc/>
    public async Task<string> EvaluateStringAsync(
        string expression,
        IReadOnlyDictionary<string, object?>? parameters = null,
        ExpressionEvaluationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(expression);
        var result = await RunWithTimeoutAsync(
            () => EvaluateCore(expression, parameters), expression, options, cancellationToken);
        return CoerceToString(result);
    }

    internal const ExpressionOptions DefaultExpressionOptions =
        ExpressionOptions.IgnoreCaseAtBuiltInFunctions | ExpressionOptions.AllowNullParameter;

    private object? EvaluateCore(string expression, IReadOnlyDictionary<string, object?>? parameters)
    {
        try
        {
            var ast = ExpressionParseCache.GetOrParse(expression, DefaultExpressionOptions);
            var expr = new NCalc.Expression(ast, DefaultExpressionOptions);

            if (parameters is not null)
            {
                foreach (var (key, value) in parameters)
                    expr.Parameters[key] = value;
            }

            if (_functions.Count > 0)
            {
                expr.EvaluateFunction += (name, args) =>
                {
                    if (_functions.TryGetValue(name, out var function))
                        args.Result = function.Invoke(args);
                };
            }

            ExpressionParameterContext.Current.Value = parameters;
            try
            {
                return expr.Evaluate();
            }
            finally
            {
                ExpressionParameterContext.Current.Value = null;
            }
        }
        catch (OutOfMemoryException) { Environment.FailFast("Out of memory"); throw; }
        catch (Exception ex) when (ex is not ExpressionEvaluationException)
        {
            throw new ExpressionEvaluationException(
                $"Failed to evaluate expression: {ex.Message}", expression, ex);
        }
    }

    private static async Task<object?> RunWithTimeoutAsync(
        Func<object?> evaluate,
        string expression,
        ExpressionEvaluationOptions? options,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var span = OtTraces.Source.Timed(OtTraces.Evaluate, OtMetrics.EvaluationDuration);

        OtMetrics.Evaluations.Add(1);

        var effective = (options ?? ExpressionEvaluationOptions.Default).EffectiveTimeout;

        try
        {
            if (effective == Timeout.InfiniteTimeSpan)
                return evaluate();

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(effective);
            return await Task.Run(evaluate, cts.Token);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException) when (effective != Timeout.InfiniteTimeSpan
                                                  && !cancellationToken.IsCancellationRequested)
        {
            OtMetrics.Timeouts.Add(1);
            span.Error("timeout");
            throw new ExpressionEvaluationException(
                $"Expression evaluation timed out after {effective.TotalMilliseconds:F0}ms.",
                expression);
        }
        catch (OutOfMemoryException) { Environment.FailFast("Out of memory"); throw; }
        catch (Exception ex)
        {
            span.Error(ex.Message);
            throw;
        }
    }

    private static bool CoerceToBoolean(object? value, string expression) => value switch
    {
        null => false,
        bool b => b,
        string s => ParseBoolString(s, expression),
        char c => c != '\0',
        sbyte or byte or short or ushort or int or uint or long or ulong or nint or nuint
        or float or double or decimal => InvariantDoubleForBool(value, expression) != 0.0,
        _ => throw new ExpressionEvaluationException(
            $"Cannot convert result of type {value.GetType().Name} to Boolean.", expression)
    };

    private static double InvariantDoubleForBool(object value, string expression)
    {
        try
        {
            return ((IConvertible)value).ToDouble(CultureInfo.InvariantCulture);
        }
        catch (Exception ex) when (ex is InvalidCastException or FormatException or OverflowException)
        {
            throw new ExpressionEvaluationException(
                $"Cannot convert result to Boolean (numeric): {ex.Message}", expression, ex);
        }
    }

    private static bool ParseBoolString(string value, string expression)
    {
        return value.Trim().ToLowerInvariant() switch
        {
            "true" or "yes" or "on" => true,
            "false" or "no" or "off" => false,
            _ => throw new ExpressionEvaluationException(
                $"Cannot convert string \"{value}\" to Boolean. " +
                "Expected one of: true, yes, on, false, no, off.",
                expression)
        };
    }

    private static string CoerceToString(object? value) => value switch
    {
        null => "",
        string s => s,
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? ""
    };
}