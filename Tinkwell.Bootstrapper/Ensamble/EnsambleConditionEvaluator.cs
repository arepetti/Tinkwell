using System.Runtime.InteropServices;
using Microsoft.Extensions.Configuration;
using Superpower;
using Tinkwell.Bootstrapper.Expressions;
using Tinkwell.Bootstrapper.Hosting;

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
}
