using System.Globalization;
using Tinkwell.Bootstrapper.Ensamble;

namespace Tinkwell.Supervisor;

static class RunnerDefinitionExtensions
{
    public static bool ShouldKeepAlive(this RunnerDefinition definition, bool defaultValue = true)
        => GetOptionValue(definition, "keep-alive", defaultValue);

    private static bool GetOptionValue(RunnerDefinition definition, string key, bool defaultValue)
    {
        if (definition.Properties.TryGetValue(key, out var value) && value is not null)
            return Convert.ToBoolean(value, CultureInfo.InvariantCulture);

        return defaultValue;
    }
}