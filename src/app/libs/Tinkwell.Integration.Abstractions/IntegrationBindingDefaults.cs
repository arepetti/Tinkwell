namespace Tinkwell.Integration;

/// <summary>
/// Well-known defaults for integration binding assembly loading.
/// </summary>
public static class IntegrationBindingDefaults
{
    /// <summary>
    /// The default assembly loaded when a <c>bind</c> block omits the
    /// <c>from</c> modifier. Contains the built-in bindings (store,
    /// measures, events, MQTT, CoAP).
    /// </summary>
    public const string DefaultAssembly = "Tinkwell.Integrations";
}
