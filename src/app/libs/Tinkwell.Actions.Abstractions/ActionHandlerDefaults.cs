namespace Tinkwell.Actions.Abstractions;

/// <summary>
/// Well-known defaults for action handler assembly loading.
/// </summary>
public static class ActionHandlerDefaults
{
    /// <summary>
    /// The default assembly loaded when a <c>do</c> block omits the
    /// <c>from</c> modifier. Contains the built-in external handlers
    /// (store, measures, MQTT, CoAP).
    /// </summary>
    public const string DefaultAssembly = "Tinkwell.Actions";
}
