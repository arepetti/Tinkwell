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

    public IEnumerable<RunnerDefinition> Filter(IEnumerable<RunnerDefinition> definitions)
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
        parameters.TryAdd("platform", ResolvePlatform());
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

        static string ResolvePlatform()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return "windows";

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                return "linux";

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                return "osx";

            if (RuntimeInformation.IsOSPlatform(OSPlatform.FreeBSD))
                return "bsd";

            return "other";
        }
    }

    private readonly IConfiguration? _configuration;
    private readonly IExpressionEvaluator _expressionEvaluator;
}
