namespace Tinkwell.Modbus;

/// <summary>
/// Minimal Modbus client supporting register read/write operations.
/// </summary>
/// <remarks>
/// <para>Implements a subset of the Modbus Application Protocol as defined in
/// <em>MODBUS Application Protocol Specification V1.1b3</em> (Modbus.org, 2012),
/// Section 4.3 — specifically function codes for register access.</para>
/// <para>Concrete implementations provide the transport layer:
/// <see cref="ModbusTcpClient"/> for Modbus TCP (MBAP framing, with request serialization),
/// <see cref="UnsynchronizedModbusTcpClient"/> for Modbus TCP without internal locking, and
/// <see cref="ModbusRtuClient"/> for Modbus RTU (serial with CRC-16).</para>
/// </remarks>
/// <example>
/// <para>Typical Modbus TCP use: create a client, connect, read holding registers, decode, then dispose with <c>await using</c>.</para>
/// <code language="csharp">
/// await using var client = new ModbusTcpClient("192.168.1.10");
/// await client.ConnectAsync();
/// ushort[] regs = await client.ReadHoldingRegistersAsync(1, 0x0000, 2);
/// float value = RegisterDecoder.ToFloat32BigEndian(regs[0], regs[1]);
/// </code>
/// </example>
public interface IModbusClient : IAsyncDisposable
{
    /// <summary>
    /// Opens the connection to the Modbus device or network.
    /// </summary>
    /// <example>
    /// <para>Call <c>ConnectAsync</c> before reads or writes; catch transport errors from the host or serial layer.</para>
    /// <code language="csharp">
    /// try
    /// {
    ///     await client.ConnectAsync(cancellationToken);
    /// }
    /// catch (System.Net.Sockets.SocketException)
    /// {
    ///     // e.g. connection refused, host unreachable
    /// }
    /// </code>
    /// </example>
    /// <param name="ct">Token that cancels the TCP socket connect or serial port open attempt.</param>
    /// <exception cref="System.Net.Sockets.SocketException">TCP connection refused or timed out.</exception>
    /// <exception cref="System.IO.IOException">Serial port could not be opened.</exception>
    Task ConnectAsync(CancellationToken ct = default);

    /// <summary>
    /// Reads one or more holding registers (function code 0x03).
    /// </summary>
    /// <example>
    /// <para>Read two consecutive holding registers and decode them as a big-endian single-precision float.</para>
    /// <code language="csharp">
    /// ushort[] regs = await client.ReadHoldingRegistersAsync(slaveId: 1, startAddress: 0x1000, count: 2);
    /// float level = RegisterDecoder.ToFloat32BigEndian(regs[0], regs[1]);
    /// </code>
    /// </example>
    /// <param name="slaveId">
    /// Unicast unit address 1–247. Address 0 (broadcast) is supported only on RTU:
    /// the frame is sent but no response is read and a zero-filled array is returned.
    /// On TCP, slave 0 throws <see cref="NotSupportedException"/>.
    /// </param>
    /// <param name="startAddress">Starting register address (0x0000–0xFFFF).</param>
    /// <param name="count">Number of registers to read (1–125).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Array of <paramref name="count"/> register values in network (big-endian) order.</returns>
    /// <exception cref="ModbusException">The device returned an exception response or a communication error occurred.</exception>
    /// <exception cref="NotSupportedException">Broadcast (slave 0) on Modbus TCP.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="slaveId"/> is 248–255, or <paramref name="count"/> is not 1–125.</exception>
    /// <exception cref="InvalidOperationException">The client is not connected.</exception>
    Task<ushort[]> ReadHoldingRegistersAsync(
        byte slaveId, ushort startAddress, ushort count, CancellationToken ct = default);

    /// <summary>
    /// Reads one or more input registers (function code 0x04).
    /// </summary>
    /// <example>
    /// <para>Read one input register that stores temperature in tenths of a degree Celsius and scale to degrees.</para>
    /// <code language="csharp">
    /// ushort[] raw = await client.ReadInputRegistersAsync(1, 0x2000, 1);
    /// double celsius = RegisterDecoder.ToInt16(raw[0]) * 0.1;
    /// </code>
    /// </example>
    /// <param name="slaveId">
    /// Unicast unit address 1–247. Address 0 (broadcast) is not valid for input
    /// register reads and throws <see cref="ArgumentOutOfRangeException"/>.
    /// </param>
    /// <param name="startAddress">Starting register address (0x0000–0xFFFF).</param>
    /// <param name="count">Number of registers to read (1–125).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Array of <paramref name="count"/> register values in network (big-endian) order.</returns>
    /// <exception cref="ModbusException">The device returned an exception response or a communication error occurred.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="slaveId"/> is 0 or greater than 247, or <paramref name="count"/> is not 1–125.</exception>
    /// <exception cref="InvalidOperationException">The client is not connected.</exception>
    Task<ushort[]> ReadInputRegistersAsync(
        byte slaveId, ushort startAddress, ushort count, CancellationToken ct = default);

    /// <summary>
    /// Writes a single holding register (function code 0x06).
    /// </summary>
    /// <example>
    /// <para>Write a 16-bit heating setpoint in the device’s fixed-point units (e.g. tenths of a degree).</para>
    /// <code language="csharp">
    /// const ushort setpointTenths = 215; // 21.5 °C
    /// await client.WriteSingleRegisterAsync(1, address: 0x3000, value: setpointTenths);
    /// </code>
    /// </example>
    /// <param name="slaveId">
    /// Unicast unit address 1–247. Address 0 (broadcast) is supported only on RTU:
    /// the frame is sent but no response is read. On TCP, slave 0 throws
    /// <see cref="NotSupportedException"/>.
    /// </param>
    /// <param name="address">Register address (0x0000–0xFFFF).</param>
    /// <param name="value">16-bit value to write.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="ModbusException">The device returned an exception response or a communication error occurred.</exception>
    /// <exception cref="NotSupportedException">Broadcast (slave 0) on Modbus TCP.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="slaveId"/> is 248–255.</exception>
    /// <exception cref="InvalidOperationException">The client is not connected.</exception>
    Task WriteSingleRegisterAsync(
        byte slaveId, ushort address, ushort value, CancellationToken ct = default);

    /// <summary>
    /// Gets a value indicating whether the transport connection is currently open.
    /// </summary>
    bool IsConnected { get; }
}
