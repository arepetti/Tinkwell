using System.Globalization;
using Tinkwell.Bootstrapper.Ensamble;

namespace Tinkwell.Supervisor;

static class RunnerDefinitionExtensions
{
    public static bool ShouldKeepAlive(this RunnerDefinition definition, bool defaultValue = true)
        => GetOptionValue(definition, "keep_alive", defaultValue);

    public static bool IsBlockingActivation(this RunnerDefinition definition, bool defaultValue = true)
        => GetActivationValue(definition, "mode", "non_blocking").Equals("blocking", StringComparison.Ordinal);

    private static bool GetOptionValue(RunnerDefinition definition, string key, bool defaultValue)
    {
        if (definition.Properties.TryGetValue(key, out var value) && value is not null)
            return Convert.ToBoolean(value, CultureInfo.InvariantCulture);

        return defaultValue;
    }

    private static string GetActivationValue(RunnerDefinition definition, string key, string defaultValue)
    {
        if (definition.Activation.TryGetValue(key, out var value) && value is not null)
            return value;

        return defaultValue;
    }
}