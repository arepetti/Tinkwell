namespace Tinkwell.Runlet.Mqtt;

/// <summary>
/// Cross-cutting middleware for the MQTT message pipeline. Runs after a
/// message is dequeued from the ingress channel but before the binding chain
/// executes. Register implementations in DI during <c>ConfigureServices</c>;
/// the MQTT connection manager discovers and orders them at startup.
/// </summary>
/// <remarks>
/// <para>
/// Call <c>next</c> in <see cref="InvokeAsync"/> to continue
/// the pipeline, or return without calling it to silently drop the message
/// (e.g. when a device fails an auth check).
/// </para>
/// <para>
/// Middleware can modify <see cref="MqttMessageContext.Topic"/> and
/// <see cref="MqttMessageContext.Payload"/> to rewrite or transform
/// messages before bindings see them.
/// </para>
/// </remarks>
public interface IMqttMiddleware
{
    /// <summary>
    /// Processes the MQTT message. Call <paramref name="next"/> to invoke
    /// the next middleware (or the binding chain); return without calling
    /// it to drop the message.
    /// </summary>
    Task InvokeAsync(
        MqttMessageContext context,
        Func<MqttMessageContext, CancellationToken, Task> next,
        CancellationToken ct);

    /// <summary>
    /// Controls execution order. Lower values run first (outermost).
    /// Default is 0.
    /// </summary>
    int Order => 0;
}
