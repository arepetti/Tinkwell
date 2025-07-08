using System.Runtime.InteropServices;
using Microsoft.Extensions.Configuration;
using Superpower;
using Tinkwell.Bootstrapper.Expressions;

namespace Tinkwell.Bootstrapper.Ensamble;

public sealed class EnsambleConditionEvaluator : IEnsambleConditionEvaluator
{
    public EnsambleConditionEvaluator(IConfiguration? configuration, IExpressionEvaluator expressionEvaluator)
    {
        _configuration = configuration;
        _expressionEvaluator = expressionEvaluator;
    }

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
