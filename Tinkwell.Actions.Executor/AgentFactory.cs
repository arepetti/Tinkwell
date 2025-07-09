using System.Globalization;
using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Tinkwell.Actions.Configuration.Parser;

namespace Tinkwell.Actions.Executor;

sealed class AgentFactory
{
    public IAgent Create(IServiceProvider serviceProvider, Intent intent)
    {
        var agent = (IAgent)ActivatorUtilities.CreateInstance(serviceProvider, intent.Directive.AgentType);
        var decodedPayload = JsonSerializer.Deserialize<Dictionary<string, object>>(intent.Payload);

        // Default to an "empty" object so that we can throw if trying to se properties
        var settings = agent.Settings ?? new object();

        // The definition guides which properties we are going to set, not the agent.
        // If a property is null (directly in agent.Settings or in a child) we use an empty
        // object (see above and SetPropertyValue()) to cause an error (unknown property):
        // that will cause the listener to be disable (it's not a recoverable condition, the
        // definitions are fixed in the configuration).
        CopyPropertiesFromDictionary(intent, agent, settings, intent.Directive.Parameters, decodedPayload);

        return agent;
    }

    private void CopyPropertiesFromDictionary(Intent intent, IAgent agent, object target, IReadOnlyDictionary<string, object> source, IReadOnlyDictionary<string, object>? decodedPayload)
    {
        // Only properties with [AgentProperty] can be set from the configuration (so we do not
        // need to worry about internal and/or read-only properties) and how to translate the property
        // name to the naming used in configuration.
        Dictionary<string, PropertyInfo> targetProperties = target.GetType()
            .GetProperties()
            .Select(property => (attribute: property.GetCustomAttribute<AgentPropertyAttribute>(), property))
            .Where(x => x.attribute is not null)
            .ToDictionary(x => x.attribute!.Name, x => x.property);

        foreach (var entry in source)
        {
            if (!targetProperties.TryGetValue(entry.Key, out var property))
                throw new ExecutorException($"Configuration contains property {entry.Key} for agent {agent.Name} which does not exist.");

            SetPropertyValue(intent, agent, target, property, entry, decodedPayload);
        }
    }

    private void SetPropertyValue(Intent intent, IAgent agent, object target, PropertyInfo targetProperty, KeyValuePair<string, object> source, IReadOnlyDictionary<string, object>? decodedPayload)
    {
        try
        {
            if (typeof(IReadOnlyDictionary<string, object>).IsAssignableFrom(source.Value.GetType()))
            {
                CopyPropertiesFromDictionary(
                    intent,
                    agent,
                    targetProperty.GetValue(target) ?? new object(), // Empty object to throw for unknown child properties
                    (IReadOnlyDictionary<string, object>)source.Value,
                    decodedPayload
                );
            }

            object sourceValue = source.Value;
            object targetValue = source.Value;

            if (sourceValue is IConvertible convertible && targetValue.GetType() != targetProperty.PropertyType)
            {
                if (targetProperty.GetType().IsEnum)
                    targetValue = Enum.Parse(targetProperty.PropertyType, convertible.ToString(CultureInfo.InvariantCulture), true);
                else
                    targetValue = Convert.ChangeType(convertible, targetProperty.PropertyType, CultureInfo.InvariantCulture);
            }

            targetProperty.SetValue(target, ComposeValue(intent, source.Key, source.Value, decodedPayload));
        }
        catch (Exception e) when (e is not ExecutorException)
        {
            throw new ExecutorException($"Cannot set property {source.Key} for agent {agent.Name}: {e.Message}");
        }
    }

    private static object ComposeValue(Intent intent, string propertyName, object rawValue, IReadOnlyDictionary<string, object>? args)
    {
        try
        {
            var model = new
            {
                topic = intent.Event.Topic,
                subject = intent.Event.Subject,
                verb = intent.Event.Verb,
                @object = intent.Event.Object,
                payload = args,
            };

            if (rawValue is ActionPropertyString specialString)
                return specialString.ToString(model);

            return rawValue;
        }
        catch (Exception e)
        {
            throw new ExecutorException($"Invalid expression for {propertyName}: {e.Message}", e);
        }
    }
}
