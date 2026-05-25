using System.Buffers.Binary;
using System.IO.Ports;
using System.Threading;

namespace Tinkwell.Modbus;

/// <summary>
/// Modbus RTU client over serial (RS-485 / RS-232). Frames PDUs with a slave address
/// byte and a CRC-16 checksum per the RTU transmission mode specification.
/// </summary>
/// <remarks>
/// <para>Implements the RTU framing defined in <em>MODBUS over Serial Line Specification
/// and Implementation Guide V1.02</em> (Modbus.org, 2006), Section 2.5.1.</para>
/// <para>RTU frame layout:</para>
/// <code>[Slave Address (1)][Function Code (1)][Data (0..252)][CRC Lo (1)][CRC Hi (1)]</code>
/// <para>The CRC-16 uses the polynomial 0xA001 with initial value 0xFFFF,
/// as defined in Section 2.5.1.2 of the Serial Line specification.
/// CRC bytes are transmitted low byte first.</para>
/// <para>Default serial parameters follow the specification's recommendation
/// (Section 2.5.1): 9600 baud, 8 data bits, no parity, 1 stop bit.</para>
/// <para>
/// <strong>I/O model:</strong> the serial path uses <see cref="System.IO.Ports.SerialPort"/> synchronous
/// <c>Read</c>/<c>Write</c> and <c>Thread.Sleep</c> for t3.5 inter-frame delay. <see cref="Task"/>-returning
/// members complete when those calls finish. To cancel, register <see cref="CancellationToken"/>
/// on <see cref="ConnectAsync(CancellationToken)"/> and on each method; cancellation
/// <em>aborts in-flight I/O by closing the port</em> (as recommended for long-running read loops without async serial APIs in the BCL). After cancellation, the client may be in an
/// indeterminate state; call <see cref="DisposeAsync"/> and create a new instance if the link must be re-established.
/// </para>
/// </remarks>
/// <example>
/// <para>Open a serial Modbus RTU link, read two holding registers, and decode a float in big-endian word order.</para>
/// <code language="csharp">
/// await using var client = new ModbusRtuClient("/dev/ttyUSB0", baudRate: 9600);
/// await client.ConnectAsync();
/// var regs = await client.ReadHoldingRegistersAsync(1, 0x0000, 2);
/// float vibration = RegisterDecoder.ToFloat32BigEndian(regs[0], regs[1]);
/// </code>
/// </example>
public sealed class ModbusRtuClient : ModbusClientBase
{
    private readonly string _portName;
    private readonly int _baudRate;
    private readonly Parity _parity;
    private readonly int _dataBits;
    private readonly StopBits _stopBits;
    private readonly int _readTimeoutMs;
    private SerialPort? _port;
    private int _disposeState;

    /// <summary>
    /// Initializes a new Modbus RTU client.
    /// </summary>
    /// <param name="portName">
    /// Operating-system serial port name (e.g. <c>/dev/ttyUSB0</c> on Linux,
    /// <c>COM3</c> on Windows).
    /// </param>
    /// <param name="baudRate">
    /// Baud rate. Common values are 9600 (default per spec Section 2.5.1) and 19200.
    /// Must match the device configuration.
    /// </param>
    /// <param name="parity">
    /// Parity mode. Default is <see cref="Parity.None"/>. When parity is <c>None</c>,
    /// 2 stop bits should be used per Section 2.5.1, but many devices accept 1 stop bit.
    /// </param>
    /// <param name="dataBits">Number of data bits (typically 8).</param>
    /// <param name="stopBits">Stop bits. Default is <see cref="StopBits.One"/>.</param>
    /// <param name="readTimeoutMs">
    /// Read timeout in milliseconds. If no response arrives within this period,
    /// a <see cref="ModbusException"/> is thrown. Also used for write timeout.
    /// </param>
    /// <exception cref="ArgumentException"><paramref name="portName"/> is null, empty, or only whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="baudRate"/> or <paramref name="readTimeoutMs"/> is not positive.</exception>
    public ModbusRtuClient(
        string portName,
        int baudRate = 9600,
        Parity parity = Parity.None,
        int dataBits = 8,
        StopBits stopBits = StopBits.One,
        int readTimeoutMs = 1000)
    {
        ValidateHostOrPortName(portName, nameof(portName));
        ValidateBaudRate(baudRate, nameof(baudRate));
        ValidateReadTimeoutMs(readTimeoutMs, nameof(readTimeoutMs));

        _portName = portName;
        _baudRate = baudRate;
        _parity = parity;
        _dataBits = dataBits;
        _stopBits = stopBits;
        _readTimeoutMs = readTimeoutMs;
    }

    /// <inheritdoc />
    protected override bool IsModbusTcpTransport => false;

    /// <inheritdoc />
    public override bool IsConnected => Volatile.Read(ref _disposeState) == 0 && _port is { IsOpen: true };

    /// <inheritdoc />
    protected override bool IsDisposed => Volatile.Read(ref _disposeState) != 0;

    /// <inheritdoc />
    public override Task ConnectAsync(CancellationToken ct = default)
    {
        ThrowIfDisposed();
        ct.ThrowIfCancellationRequested();
        if (IsConnected)
        {
            throw new InvalidOperationException(
                "Already connected. The Modbus RTU client may only be connected once per instance; disconnect by disposing, or use a new client for another connection.");
        }

        _port = new SerialPort(_portName, _baudRate, _parity, _dataBits, _stopBits)
        {
            ReadTimeout = _readTimeoutMs,
            WriteTimeout = _readTimeoutMs,
        };

        try
        {
            _port.Open();
        }
        catch
        {
            _port?.Dispose();
            _port = null;
            throw;
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposeState, 1) != 0)
            return ValueTask.CompletedTask;

        try
        {
            _port?.Close();
        }
        catch
        {
            // best-effort close
        }

        _port?.Dispose();
        _port = null;
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Broadcast read holding: transmits FC 0x03 and skips the read phase (no response is expected
    /// on a broadcast; result registers are default).
    /// </summary>
    protected override Task<ushort[]> ReadHoldingRegistersBroadcastRtuAsync(
        ushort startAddress,
        ushort count,
        CancellationToken ct)
    {
        ThrowIfDisposed();
        ct.ThrowIfCancellationRequested();
        EnsureConnected();

        var pdu = new byte[5];
        pdu[0] = 0x03;
        BinaryPrimitives.WriteUInt16BigEndian(pdu.AsSpan(1), startAddress);
        BinaryPrimitives.WriteUInt16BigEndian(pdu.AsSpan(3), count);

        RtuWriteFrameAndSkipResponse(pdu, ct);
        return Task.FromResult(new ushort[count]);
    }

    /// <summary>
    /// Broadcast write: writes FC 0x06 request with unit ID 0 and does not read a response.
    /// </summary>
    protected override Task WriteBroadcastPduRtuNoResponseAsync(byte[] pdu, CancellationToken ct)
    {
        ThrowIfDisposed();
        ct.ThrowIfCancellationRequested();
        EnsureConnected();
        RtuWriteFrameAndSkipResponse(pdu, ct);
        return Task.CompletedTask;
    }

    private void RtuWriteFrameAndSkipResponse(byte[] pdu, CancellationToken ct)
    {
        if (_port is null)
            throw new InvalidOperationException("Not connected. Call ConnectAsync first.");
        RtuBuildAndWrite(0, pdu, _port, ct, expectResponse: false);
    }

    /// <inheritdoc />
    protected override Task<byte[]> SendRequestAsync(
        byte slaveId,
        byte[] pdu,
        CancellationToken ct)
    {
        ThrowIfDisposed();
        ct.ThrowIfCancellationRequested();
        if (_port is null)
            throw new InvalidOperationException("Not connected. Call ConnectAsync first.");

        using (ct.Register(
                   static o =>
                   {
                       try
                       {
                           ((SerialPort?)o)?.Close();
                       }
                       catch
                       {
                           // Best-effort abort: closing the port cancels blocking Read/Write.
                       }
                   },
                   _port))
        {
            return Task.FromResult(
                RtuBuildAndTransceiveResponsePdu(slaveId, pdu, _port, ct));
        }
    }

    private static byte[] RtuBuildAndTransceiveResponsePdu(
        byte slaveId,
        byte[] pdu,
        SerialPort port,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        RtuBuildAndWrite(slaveId, pdu, port, ct, expectResponse: true);
        return RtuReadResponseAndStripCrcToPdu(port, ct);
    }

    private static void RtuBuildAndWrite(
        byte slaveId,
        byte[] pdu,
        SerialPort port,
        CancellationToken ct,
        bool expectResponse)
    {
        var frame = new byte[1 + pdu.Length + 2];
        frame[0] = slaveId;
        pdu.AsSpan().CopyTo(frame.AsSpan(1));
        var crc = Crc16.Compute(frame.AsSpan(0, 1 + pdu.Length));
        frame[^2] = (byte)(crc & 0xFF);
        frame[^1] = (byte)(crc >> 8);

        port.DiscardInBuffer();
        port.Write(frame, 0, frame.Length);

        if (!expectResponse)
            return;

        // Inter-frame silence: t3.5 = 3.5 character times.
        // At 9600 baud (11 bits/char): 3.5 * 11 / 9600 ~= 4 ms.
        // For baud rates above 19200, the spec (Section 2.5.1.1) fixes t3.5 at 1.75 ms;
        // we use a minimum of 4 ms since Thread.Sleep resolution is limited.
        var delayMs = Math.Max(4, (int)(40_000.0 / port.BaudRate));
        Thread.Sleep(delayMs);
        ct.ThrowIfCancellationRequested();
    }

    private static byte[] RtuReadResponseAndStripCrcToPdu(SerialPort port, CancellationToken ct)
    {
        var headerBuf = new byte[3];
        RtuReadExact(port, headerBuf, 0, 3, ct);

        int dataLength;
        if ((headerBuf[1] & 0x80) != 0)
        {
            dataLength = 1;
        }
        else if (headerBuf[1] is 0x03 or 0x04)
        {
            dataLength = headerBuf[2] + 2;
        }
        else if (headerBuf[1] == 0x06)
        {
            dataLength = 4 + 2;
        }
        else
        {
            throw new ModbusException($"Unsupported function code in response: 0x{headerBuf[1]:X2}");
        }

        var remaining = new byte[dataLength];
        RtuReadExact(port, remaining, 0, dataLength, ct);

        var fullResponse = new byte[3 + dataLength];
        headerBuf.AsSpan().CopyTo(fullResponse);
        remaining.AsSpan().CopyTo(fullResponse.AsSpan(3));

        var computedCrc = Crc16.Compute(fullResponse.AsSpan(0, fullResponse.Length - 2));
        var receivedCrc = (ushort)(fullResponse[^2] | (fullResponse[^1] << 8));
        if (computedCrc != receivedCrc)
            throw new ModbusException("CRC mismatch in response — possible noise or wiring issue");

        var responsePdu = new byte[fullResponse.Length - 3];
        Array.Copy(fullResponse, 1, responsePdu, 0, responsePdu.Length);
        return responsePdu;
    }

    private static void RtuReadExact(SerialPort? port, byte[] buffer, int offset, int count, CancellationToken ct)
    {
        if (port is null)
            throw new ObjectDisposedException(nameof(SerialPort));

        var read = 0;
        while (read < count)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var n = port.Read(buffer, offset + read, count - read);
                if (n == 0)
                    throw new ModbusException("No data received from device");
                read += n;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (ObjectDisposedException)
            {
                if (ct.IsCancellationRequested)
                    throw new OperationCanceledException(ct);
                throw;
            }
            catch (InvalidOperationException)
            {
                if (ct.IsCancellationRequested)
                    throw new OperationCanceledException(ct);
                throw;
            }
            catch (TimeoutException)
            {
                if (ct.IsCancellationRequested)
                    throw new OperationCanceledException(ct);
                throw new ModbusException("Read timeout — no response from device");
            }
        }
    }
}
