using Tinkwell.Encoding;

namespace Tinkwell.Lwm2m.Server;

/// <summary>
/// Handles read and write operations on a single LwM2M resource.
/// </summary>
/// <remarks>
/// Implementations may be invoked concurrently (multiple CoAP requests). Handlers that mutate
/// shared state should synchronize access.
/// <example>
/// <para>Read returns the last sensor sample; write applies a high-alarm limit (configuration).</para>
/// <code language="csharp">
/// public sealed class TemperatureAndAlarmHandler : ILwm2mResourceHandler
/// {
///     private double _lastCelsius;
///     private int _maxAlarmC;
///     public PayloadValue? OnRead() => PayloadValue.FromFloat(_lastCelsius);
///     public void OnWrite(PayloadValue value) { _maxAlarmC = (int)value.AsLong(); }
/// }
/// </code>
/// </example>
/// </remarks>
public interface ILwm2mResourceHandler
{
    /// <summary>
    /// Called when a client reads the resource. Return <see langword="null"/> if the resource
    /// has no current value, which is translated to 4.04 Not Found for the read response.
    /// </summary>
    /// <example>
    /// <para>Return the most recent reading from a sensor, or <see langword="null"/> if nothing is available yet.</para>
    /// <code language="csharp">
    /// public PayloadValue? OnRead() =>
    ///     _lastLux is { } n ? PayloadValue.FromInteger(n) : null;
    /// </code>
    /// </example>
    PayloadValue? OnRead();

    /// <summary>
    /// Called when a client writes a new value to the resource.
    /// </summary>
    /// <param name="value">The decoded payload for this resource write.</param>
    /// <example>
    /// <para>Apply a written configuration value, such as a setpoint or threshold, after decoding.</para>
    /// <code language="csharp">
    /// public void OnWrite(PayloadValue value)
    /// {
    ///     var celsius = (int)value.AsLong();
    ///     _device.SetMaxTempAlarmC(celsius);
    /// }
    /// </code>
    /// </example>
    void OnWrite(PayloadValue value);
}
