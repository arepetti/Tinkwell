using System.Buffers.Binary;

namespace Tinkwell.Modbus;

/// <summary>
/// Common Modbus client logic: parameter validation, register read/decode, exception mapping,
/// and function-code template methods. Transport-specific code implements
/// <see cref="SendRequestAsync"/>.
/// </summary>
/// <remarks>
/// <para>Implements a subset of the Modbus Application Protocol as defined in
/// <em>MODBUS Application Protocol Specification V1.1b3</em> (Modbus.org, 2012), Sections
/// 4.3, 6.3, 6.4, 6.6, and 7. Concrete subclasses add transport framing (Modbus RTU, Modbus
/// TCP) per the respective transport specifications.</para>
/// </remarks>
public abstract class ModbusClientBase : IModbusClient
{
    /// <summary>
    /// When <see langword="true"/>, this client uses the Modbus TCP (MBAP) transport.
    /// When <see langword="false"/>, this client uses Modbus RTU.
    /// </summary>
    protected abstract bool IsModbusTcpTransport { get; }

    /// <inheritdoc />
    public abstract bool IsConnected { get; }

    /// <inheritdoc />
    public abstract Task ConnectAsync(CancellationToken ct = default);

    /// <inheritdoc />
    public abstract ValueTask DisposeAsync();

    /// <summary>
    /// Sends a request PDU to the target unit and returns the response PDU. Implementations
    /// perform transport-specific framing, I/O, and unframing. For Modbus RTU, this
    /// typically blocks on synchronous serial I/O; cancellation may close the port to abort.
    /// </summary>
    /// <param name="slaveId">Modbus address (0 for broadcast; 1–247 for unicast).</param>
    /// <param name="pdu">The Protocol Data Unit: function code and payload, max 253 bytes per <em>Modbus Application Protocol V1.1b3</em>, Section 4.3.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>Response PDU (no transport header or trailer).</returns>
    protected abstract Task<byte[]> SendRequestAsync(
        byte slaveId,
        byte[] pdu,
        CancellationToken ct);

    /// <summary>
    /// Called when a Modbus RTU broadcast (slave address 0) write is performed: transmits the
    /// request with no read phase. The default implementation is not used on Modbus TCP; see
    /// <see cref="IsModbusTcpTransport"/>.
    /// </summary>
    /// <param name="pdu">The FC 0x06 request PDU to transmit.</param>
    /// <param name="ct">The cancellation token.</param>
    protected virtual Task WriteBroadcastPduRtuNoResponseAsync(byte[] pdu, CancellationToken ct) =>
        throw new InvalidOperationException("Broadcast write without response is only supported for Modbus RTU.");

    /// <summary>
    /// Called for Modbus RTU broadcast (slave 0) read holding (FC 0x03): sends the read request
    /// and skips the response read, returning a zero-filled result as no data is available from the bus.
    /// </summary>
    /// <param name="startAddress">Starting register address (0x0000–0xFFFF).</param>
    /// <param name="count">Number of registers (1–125; already validated).</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A zero-filled array of length <paramref name="count"/>.</returns>
    protected virtual Task<ushort[]> ReadHoldingRegistersBroadcastRtuAsync(
        ushort startAddress,
        ushort count,
        CancellationToken ct) =>
        throw new InvalidOperationException("Holding read broadcast (unit ID 0) is only valid for Modbus RTU.");

    /// <inheritdoc />
    public virtual async Task<ushort[]> ReadHoldingRegistersAsync(
        byte slaveId,
        ushort startAddress,
        ushort count,
        CancellationToken ct = default)
    {
        ThrowIfDisposed();
        ThrowIfCancelled(ct);
        ValidateCount(count, nameof(count));

        if (slaveId == 0)
        {
            if (IsModbusTcpTransport)
            {
                throw new NotSupportedException(
                    "Broadcast (unit ID 0) is not supported for Modbus TCP. Use Modbus RTU, or a unicast address 1–247 per MODBUS Messaging on TCP/IP, Section 4.1 (unit ID in the MBAP).");
            }

            return await ReadHoldingRegistersBroadcastRtuAsync(startAddress, count, ct)
                .ConfigureAwait(false);
        }

        ValidateUnicastSlaveId(slaveId, nameof(slaveId));
        return await ReadRegistersFromPduAsync(0x03, slaveId, startAddress, count, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public virtual async Task<ushort[]> ReadInputRegistersAsync(
        byte slaveId,
        ushort startAddress,
        ushort count,
        CancellationToken ct = default)
    {
        ThrowIfDisposed();
        ThrowIfCancelled(ct);
        ValidateCount(count, nameof(count));

        if (slaveId == 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(slaveId),
                "Input register reads (FC 0x04) use a unicast address 1–247. Unit ID 0 (broadcast) is not valid for this function per typical Modbus use.");
        }

        ValidateUnicastSlaveId(slaveId, nameof(slaveId));
        return await ReadRegistersFromPduAsync(0x04, slaveId, startAddress, count, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public virtual async Task WriteSingleRegisterAsync(
        byte slaveId,
        ushort address,
        ushort value,
        CancellationToken ct = default)
    {
        ThrowIfDisposed();
        ThrowIfCancelled(ct);
        if (IsModbusTcpTransport && slaveId == 0)
        {
            throw new NotSupportedException(
                "Broadcast (unit ID 0) is not supported for Modbus TCP. Use Modbus RTU, or a unicast address 1–247 per MODBUS Messaging on TCP/IP.");
        }

        if (slaveId == 0 && !IsModbusTcpTransport)
        {
            var pdu = new byte[5];
            pdu[0] = 0x06;
            BinaryPrimitives.WriteUInt16BigEndian(pdu.AsSpan(1), address);
            BinaryPrimitives.WriteUInt16BigEndian(pdu.AsSpan(3), value);
            await WriteBroadcastPduRtuNoResponseAsync(pdu, ct).ConfigureAwait(false);
            return;
        }

        ValidateUnicastSlaveId(slaveId, nameof(slaveId));

        // FC 0x06 request PDU: [0x06][address_hi][address_lo][value_hi][value_lo]
        // Per Modbus Application Protocol V1.1b3, Section 6.6.
        var writePdu = new byte[5];
        writePdu[0] = 0x06;
        BinaryPrimitives.WriteUInt16BigEndian(writePdu.AsSpan(1), address);
        BinaryPrimitives.WriteUInt16BigEndian(writePdu.AsSpan(3), value);

        var response = await SendRequestAsync(slaveId, writePdu, ct).ConfigureAwait(false);
        ValidateWriteSingleRegisterEcho(response, address, value);
    }

    /// <summary>
    /// Throws <see cref="ObjectDisposedException"/> if this client has been disposed.
    /// </summary>
    /// <exception cref="ObjectDisposedException">The client has been disposed.</exception>
    protected void ThrowIfDisposed()
    {
        if (IsDisposed)
            throw new ObjectDisposedException(GetType().Name);
    }

    /// <summary>
    /// When <see langword="true"/>, the instance has been disposed and must not be used.
    /// </summary>
    protected abstract bool IsDisposed { get; }

    /// <summary>
    /// Ensures a transport connection is open before a request, using the same rules as
    /// <see cref="IModbusClient.IsConnected"/>. The default throws if not connected; overrides may extend this.
    /// </summary>
    /// <exception cref="InvalidOperationException">The client is not connected.</exception>
    protected virtual void EnsureConnected()
    {
        if (!IsConnected)
            throw new InvalidOperationException("Not connected. Call ConnectAsync first.");
    }

    private async Task<ushort[]> ReadRegistersFromPduAsync(
        byte functionCode,
        byte slaveId,
        ushort startAddress,
        ushort count,
        CancellationToken ct)
    {
        EnsureConnected();
        ThrowIfCancelled(ct);
        var pdu = new byte[5];
        pdu[0] = functionCode;
        BinaryPrimitives.WriteUInt16BigEndian(pdu.AsSpan(1), startAddress);
        BinaryPrimitives.WriteUInt16BigEndian(pdu.AsSpan(3), count);

        var response = await SendRequestAsync(slaveId, pdu, ct).ConfigureAwait(false);
        return DecodeHoldingOrInputRegisters(response, functionCode, count);
    }

    /// <summary>
    /// Validates an FC 0x03 or FC 0x04 read response, checks <c>byte_count = register_count * 2</c>,
    /// and decodes big-endian register values. Per <em>Modbus Application Protocol V1.1b3</em>, Sections 6.3 and 6.4.
    /// </summary>
    /// <param name="responsePdu">The response PDU (function code, byte count, and data).</param>
    /// <param name="functionCode">0x03 or 0x04.</param>
    /// <param name="requestedCount">The number of registers requested; must match the data length in the response.</param>
    /// <returns>Decoded register array.</returns>
    /// <exception cref="ModbusException">The response length or <c>byte_count</c> is invalid.</exception>
    protected static ushort[] DecodeHoldingOrInputRegisters(
        ReadOnlySpan<byte> responsePdu,
        byte functionCode,
        int requestedCount)
    {
        ValidateResponse(responsePdu, functionCode);
        if (responsePdu.Length < 2)
            throw new ModbusException("Response too short: missing byte count for FC 0x03/0x04.");

        var byteCount = responsePdu[1];
        if ((byteCount & 1) != 0)
            throw new ModbusException("Invalid byte count in FC 0x03/0x04 response: must be even (2 bytes per 16-bit register).");

        var expected = checked((byte)(requestedCount * 2));
        if (byteCount != expected)
        {
            throw new ModbusException(
                $"Invalid byte count 0x{byteCount:X2} in FC 0x{functionCode:X2} response: expected 0x{expected:X2} (register count {requestedCount} * 2) per Modbus Application Protocol V1.1b3, Sections 6.3/6.4.");
        }

        if (responsePdu.Length < 2 + byteCount)
        {
            throw new ModbusException(
                $"Response PDU too short: need {2 + byteCount} bytes (function, byte count, and data), have {responsePdu.Length}.");
        }

        var registers = new ushort[byteCount / 2];
        for (var i=0; i < registers.Length; ++i)
        {
            registers[i] = BinaryPrimitives.ReadUInt16BigEndian(
                responsePdu[(2 + i * 2)..]);
        }

        return registers;
    }

    /// <summary>
    /// Validates the register count (1–125) for FC 0x03, 0x04, and their requests.
    /// Per <em>Modbus Application Protocol V1.1b3</em>, Section 4.1 (quantity limits).
    /// </summary>
    /// <param name="count">The requested quantity of registers.</param>
    /// <param name="paramName">The parameter name for the exception.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is not in 1..125.</exception>
    protected static void ValidateCount(ushort count, string paramName)
    {
        if (count is < 1 or > 125)
            throw new ArgumentOutOfRangeException(paramName, count, "Register count must be between 1 and 125 (per Modbus Application Protocol V1.1b3, Section 4.1).");
    }

    /// <summary>
    /// Validates a unicast unit address: 1–247 (0 is reserved for broadcast; 248–255 / 0xF8–0xFF
    /// are reserved in the Modbus serial line specification).
    /// </summary>
    /// <param name="slaveId">The slave (unit) ID.</param>
    /// <param name="paramName">The parameter name for the exception.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="slaveId"/> is not 1..247.</exception>
    protected static void ValidateUnicastSlaveId(byte slaveId, string paramName)
    {
        if (slaveId == 0 || slaveId > 247)
        {
            throw new ArgumentOutOfRangeException(
                paramName,
                slaveId,
                "Unicast unit ID must be in the range 1–247 (0 is broadcast; 248–255 are reserved per the Modbus serial line specification).");
        }
    }

    /// <summary>
    /// Validates a non-null, non-empty host or port name string.
    /// </summary>
    /// <param name="name">Host name, IP string, or serial port name.</param>
    /// <param name="paramName">The parameter name for the exception.</param>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="name"/> is empty or only whitespace.</exception>
    protected static void ValidateHostOrPortName(string name, string paramName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name, paramName);
    }

    /// <summary>
    /// Validates a positive read timeout in milliseconds.
    /// </summary>
    /// <param name="readTimeoutMs">Timeout in milliseconds.</param>
    /// <param name="paramName">The parameter name for the exception.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="readTimeoutMs"/> is not positive.</exception>
    protected static void ValidateReadTimeoutMs(int readTimeoutMs, string paramName)
    {
        if (readTimeoutMs <= 0)
            throw new ArgumentOutOfRangeException(paramName, readTimeoutMs, "Read (and write) timeout must be positive.");
    }

    /// <summary>
    /// Validates a TCP or UDP port number.
    /// </summary>
    /// <param name="port">The port to validate.</param>
    /// <param name="paramName">The parameter name for the exception.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="port"/> is not in 1..65535.</exception>
    protected static void ValidateTcpPort(int port, string paramName)
    {
        if ((uint)port is < 1u or > 65535u)
            throw new ArgumentOutOfRangeException(paramName, port, "Port must be in the range 1–65535.");
    }

    /// <summary>
    /// Validates a positive serial baud rate.
    /// </summary>
    /// <param name="baudRate">Baud rate in bits per second.</param>
    /// <param name="paramName">The parameter name for the exception.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="baudRate"/> is not positive.</exception>
    protected static void ValidateBaudRate(int baudRate, string paramName)
    {
        if (baudRate <= 0)
            throw new ArgumentOutOfRangeException(paramName, baudRate, "Baud rate must be positive.");
    }

    private static void ThrowIfCancelled(CancellationToken ct) => ct.ThrowIfCancellationRequested();

    /// <summary>
    /// Validates the FC 0x06 (Write Single Register) response: the device must echo back the
    /// exact request PDU — <c>[0x06][address_hi][address_lo][value_hi][value_lo]</c>.
    /// Per <em>Modbus Application Protocol V1.1b3</em>, Section 6.6.
    /// </summary>
    /// <param name="responsePdu">The response PDU from the device.</param>
    /// <param name="expectedAddress">The register address that was written.</param>
    /// <param name="expectedValue">The value that was written.</param>
    /// <exception cref="ModbusException">The response does not match the request echo.</exception>
    private static void ValidateWriteSingleRegisterEcho(
        ReadOnlySpan<byte> responsePdu,
        ushort expectedAddress,
        ushort expectedValue)
    {
        ValidateResponse(responsePdu, 0x06);

        if (responsePdu.Length < 5)
        {
            throw new ModbusException(
                $"FC 0x06 response too short: expected 5 bytes (echo of request), received {responsePdu.Length}.");
        }

        var echoAddress = BinaryPrimitives.ReadUInt16BigEndian(responsePdu[1..]);
        var echoValue = BinaryPrimitives.ReadUInt16BigEndian(responsePdu[3..]);

        if (echoAddress != expectedAddress || echoValue != expectedValue)
        {
            throw new ModbusException(
                $"FC 0x06 echo mismatch: expected address=0x{expectedAddress:X4} value=0x{expectedValue:X4}, " +
                $"received address=0x{echoAddress:X4} value=0x{echoValue:X4} (per Modbus Application Protocol V1.1b3, Section 6.6).");
        }
    }

    /// <summary>
    /// Validates a response PDU: exception responses (function code with bit 7 set) and
    /// function code match. Per <em>Modbus Application Protocol V1.1b3</em>, Section 7.
    /// </summary>
    /// <param name="pdu">The response Protocol Data Unit.</param>
    /// <param name="expectedFunctionCode">The expected normal function code (without the exception bit).</param>
    /// <exception cref="ModbusException">The response is an exception, empty, or the function code does not match.</exception>
    protected static void ValidateResponse(ReadOnlySpan<byte> pdu, byte expectedFunctionCode)
    {
        if (pdu.Length == 0)
            throw new ModbusException("Empty response PDU");

        if ((pdu[0] & 0x80) != 0)
        {
            var exCode = pdu.Length > 1 ? pdu[1] : (byte)0;
            throw new ModbusException(
                $"Modbus exception 0x{exCode:X2}: {ExceptionMessage(exCode)}")
            {
                ExceptionCode = exCode,
            };
        }

        if (pdu[0] != expectedFunctionCode)
        {
            throw new ModbusException(
                $"Unexpected function code 0x{pdu[0]:X2}, expected 0x{expectedFunctionCode:X2}");
        }
    }

    /// <summary>
    /// Returns a human-readable name for standard Modbus exception codes.
    /// Per <em>Modbus Application Protocol V1.1b3</em>, Section 7, Table 2.
    /// </summary>
    /// <param name="code">The exception code from the response.</param>
    /// <returns>A short English description of the code.</returns>
    protected static string ExceptionMessage(byte code) => code switch
    {
        0x01 => "Illegal Function",
        0x02 => "Illegal Data Address",
        0x03 => "Illegal Data Value",
        0x04 => "Slave Device Failure",
        0x05 => "Acknowledge",
        0x06 => "Slave Device Busy",
        0x08 => "Memory Parity Error",
        0x0A => "Gateway Path Unavailable",
        0x0B => "Gateway Target Device Failed to Respond",
        _ => $"Unknown (0x{code:X2})",
    };
}
