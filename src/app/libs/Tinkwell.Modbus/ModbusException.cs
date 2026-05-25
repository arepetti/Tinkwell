namespace Tinkwell.Modbus;

/// <summary>
/// Thrown when a Modbus operation fails due to a communication error or a device
/// exception response.
/// </summary>
/// <remarks>
/// When the device returns an exception response (function code with bit 7 set),
/// <see cref="ExceptionCode"/> contains the Modbus exception code as defined in
/// <em>MODBUS Application Protocol Specification V1.1b3</em>, Section 7, Table 2.
/// </remarks>
public sealed class ModbusException : Exception
{
    /// <summary>
    /// Initializes a new instance of <see cref="ModbusException"/> with the specified message.
    /// </summary>
    /// <param name="message">A description of the error, or <see langword="null"/> to use a default message.</param>
    public ModbusException(string? message) : base(message ?? "A Modbus error occurred.")
    {
    }

    /// <summary>
    /// Initializes a new instance of <see cref="ModbusException"/> with the specified message
    /// and inner exception.
    /// </summary>
    /// <param name="message">A description of the error, or <see langword="null"/> to use a default message.</param>
    /// <param name="inner">The exception that caused this error.</param>
    public ModbusException(string? message, Exception inner) : base(message ?? "A Modbus error occurred.", inner)
    {
    }

    /// <summary>
    /// Gets the Modbus exception code returned by the device, if available.
    /// </summary>
    /// <remarks>
    /// Standard codes per <em>Modbus Application Protocol V1.1b3</em>, Section 7, Table 2:
    /// <list type="table">
    /// <listheader><term>Code</term><description>Meaning</description></listheader>
    /// <item><term>0x01</term><description>Illegal Function</description></item>
    /// <item><term>0x02</term><description>Illegal Data Address</description></item>
    /// <item><term>0x03</term><description>Illegal Data Value</description></item>
    /// <item><term>0x04</term><description>Slave Device Failure</description></item>
    /// <item><term>0x05</term><description>Acknowledge</description></item>
    /// <item><term>0x06</term><description>Slave Device Busy</description></item>
    /// <item><term>0x08</term><description>Memory Parity Error</description></item>
    /// <item><term>0x0A</term><description>Gateway Path Unavailable</description></item>
    /// <item><term>0x0B</term><description>Gateway Target Device Failed to Respond</description></item>
    /// </list>
    /// </remarks>
    public byte? ExceptionCode { get; init; }
}
