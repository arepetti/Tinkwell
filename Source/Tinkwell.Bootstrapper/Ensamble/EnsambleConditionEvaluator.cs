using Microsoft.Extensions.Configuration;
using Superpower;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using Tinkwell.Bootstrapper.Expressions;
using Tinkwell.Bootstrapper.Hosting;
using Tinkwell.Bootstrapper.Ipc;

namespace Tinkwell.Bootstrapper.Ensamble;

/// <summary>
/// Evaluates conditions for ensamble runner definitions and provides parameter values for evaluation.
/// </summary>
public sealed class EnsambleConditionEvaluator : IEnsambleConditionEvaluator
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EnsambleConditionEvaluator"/> class.
    /// </summary>
    /// <param name="configuration">The configuration to use for parameters.</param>
    /// <param name="expressionEvaluator">The expression evaluator to use for condition evaluation.</param>
    public EnsambleConditionEvaluator(IConfiguration? configuration, IExpressionEvaluator expressionEvaluator)
    {
        _configuration = configuration;
        _expressionEvaluator = expressionEvaluator;
    }

    /// <summary>
    /// Filters the provided definitions based on their conditions.
    /// </summary>
    /// <typeparam name="T">The type of definition, must implement <see cref="IConditionalDefinition"/>.</typeparam>
    /// <param name="definitions">The definitions to filter.</param>
    /// <returns>The filtered definitions.</returns>
    public IEnumerable<T> Filter<T>(IEnumerable<T> definitions)
        where T : IConditionalDefinition
    {
        return definitions.Where(runner =>
        {
            if (string.IsNullOrWhiteSpace(runner.Condition))
                return true;

            return _expressionEvaluator.EvaluateBool(runner.Condition, GetParameters());
        });
    }

    /// <summary>
    /// Gets the parameters used for condition evaluation.
    /// </summary>
    /// <returns>A dictionary of parameter names and values.</returns>
    public Dictionary<string, string?> GetParameters()
    {
        var parameters = ReadFromConfiguration();
        parameters.TryAdd("os_architecture", RuntimeInformation.OSArchitecture.ToString());
        parameters.TryAdd("processor_architecture", RuntimeInformation.ProcessArchitecture.ToString());
        parameters.TryAdd("platform", HostingInformation.ResolvePlatform());
        parameters.TryAdd("session_id", Environment.ProcessId.ToString());
        parameters.TryAdd("environment", IsDevelopmentEnvironment() ? "development" : "release");

        return parameters;

        Dictionary<string, string?> ReadFromConfiguration()
        {
            if (_configuration is null)
                return [];

            var section = _configuration.GetSection("Ensamble:Params");
            return section.GetChildren()
                .ToDictionary(x => x.Key, x => x.Value);
        }
    }

    private readonly IConfiguration? _configuration;
    private readonly IExpressionEvaluator _expressionEvaluator;

    private bool IsDevelopmentEnvironment()
    {
        // Forced by configuration option or environment variable.
        // If this is present then we honor whatever value there is.
        var environment = _configuration?.GetValue<string>("Ensamble:Environment")
            ?? Environment.GetEnvironmentVariable(WellKnownNames.EnvironmentEnvironmentVariable);

        if (!string.IsNullOrWhiteSpace(environment))
            return string.Equals("Development", environment, StringComparison.OrdinalIgnoreCase);

        // Inferred from [Debuggable] attached to the assembly, we use two methods
        // because neither is 100% accurate.
        var entryAssembly = Assembly.GetEntryAssembly();
        if (entryAssembly is not null)
        {
            var debuggable = entryAssembly.GetCustomAttribute<DebuggableAttribute>();
            if (debuggable?.IsJITTrackingEnabled ?? false)
                return true;

            if (debuggable?.IsJITOptimizerDisabled ?? false)
                return true;
        }

        // If this assembly has been compiled in debug then let's assume we're in development mode
#if DEBUG
        return true;
#else
        return false;
#endif
    }
}
