using System.Buffers.Binary;
using System.Net.Sockets;

namespace Tinkwell.Modbus;

/// <summary>
/// Abstract Modbus TCP client: MBAP (Modbus Application Protocol) header construction and parsing,
/// TCP <see cref="System.Net.Sockets.TcpClient"/> lifecycle, and transaction identifiers.
/// </summary>
/// <remarks>
/// <para>Implements the framing described in <em>MODBUS Messaging on TCP/IP Implementation
/// Guide V1.0b</em> (Modbus.org, 2006), Section 3.1.3, including validation of the response MBAP
/// (transaction ID, protocol ID, unit ID).</para>
/// <para>Each request is wrapped in a 7-byte MBAP header:</para>
/// <list type="table">
/// <listheader><term>Field</term><description>Bytes</description></listheader>
/// <item><term>Transaction Identifier</term><description>2 — must match the request</description></item>
/// <item><term>Protocol Identifier</term><description>2 — must be 0x0000 for standard Modbus</description></item>
/// <item><term>Length</term><description>2 — number of following bytes (Unit ID + PDU)</description></item>
/// <item><term>Unit Identifier</term><description>1 — the slave (unit) address</description></item>
/// </list>
/// <para>Default TCP port is 502, as specified in Section 4.1 of the Implementation Guide.</para>
/// <para>Threading: this type does not synchronize concurrent requests. Use
/// <see cref="ModbusTcpClient"/> for a synchronized client, or
/// <see cref="UnsynchronizedModbusTcpClient"/> for maximum throughput in single-threaded or
/// externally coordinated scenarios.</para>
/// </remarks>
/// <example>
/// <para>Obtain a client from a concrete type, connect, and perform holding-register reads; dispose when finished.</para>
/// <code language="csharp">
/// await using ModbusTcpClientBase client = new ModbusTcpClient("192.168.0.1");
/// await client.ConnectAsync();
/// ushort[] regs = await client.ReadHoldingRegistersAsync(1, 0x0100, 1);
/// </code>
/// </example>
public abstract class ModbusTcpClientBase : ModbusClientBase
{
    private readonly string _host;
    private readonly int _remotePort;

    private TcpClient? _tcp;
    private NetworkStream? _stream;
    private int _transactionId;
    private bool _disposed;

    /// <summary>
    /// Initializes a new Modbus TCP client base.
    /// </summary>
    /// <param name="host">Hostname or IP address of the Modbus TCP device or gateway.</param>
    /// <param name="port">
    /// TCP port number. Defaults to 502 per <em>MODBUS Messaging on TCP/IP
    /// Implementation Guide V1.0b</em>, Section 4.1.
    /// </param>
    /// <exception cref="ArgumentException"><paramref name="host"/> is null, empty, or only whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="port"/> is not in 1..65535.</exception>
    protected ModbusTcpClientBase(string host, int port = 502)
    {
        ValidateHostOrPortName(host, nameof(host));
        ValidateTcpPort(port, nameof(port));
        _host = host;
        _remotePort = port;
    }

    /// <inheritdoc />
    protected override bool IsModbusTcpTransport => true;

    /// <inheritdoc />
    public override bool IsConnected =>
        !_disposed
        && _tcp is { Connected: true }
        && _stream is not null;

    /// <inheritdoc />
    /// <summary>
    /// Connects the TCP client to the configured host and port.
    /// </summary>
    public override async Task ConnectAsync(CancellationToken ct = default)
    {
        ThrowIfDisposed();
        if (IsConnected)
        {
            throw new InvalidOperationException(
                "Already connected. The Modbus TCP client may only be connected once per instance; disconnect by disposing, or use a new client for another connection.");
        }

        _tcp = new TcpClient();
        try
        {
            await _tcp.ConnectAsync(_host, _remotePort, ct).ConfigureAwait(false);
            _stream = _tcp.GetStream();
        }
        catch
        {
            await TryDisposeNetworkResourcesAsync().ConfigureAwait(false);
            _tcp = null;
            _stream = null;
            throw;
        }
    }

    /// <inheritdoc />
    public override async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;
        await TryDisposeNetworkResourcesAsync().ConfigureAwait(false);
    }

    /// <inheritdoc />
    protected override bool IsDisposed => _disposed;

    private async ValueTask TryDisposeNetworkResourcesAsync()
    {
        if (_stream is not null)
        {
            await _stream.DisposeAsync().ConfigureAwait(false);
            _stream = null;
        }

        if (_tcp is not null)
        {
            _tcp.Dispose();
            _tcp = null;
        }
    }

    /// <inheritdoc />
    /// <summary>
    /// Wraps a PDU in an MBAP frame, writes it, reads the response, and validates the MBAP and PDU.
    /// </summary>
    protected override async Task<byte[]> SendRequestAsync(
        byte slaveId,
        byte[] pdu,
        CancellationToken ct)
    {
        ThrowIfDisposed();
        ct.ThrowIfCancellationRequested();

        if (_stream is null)
            throw new InvalidOperationException("Not connected. Call ConnectAsync first.");

        if (pdu is null)
            throw new ArgumentNullException(nameof(pdu));

        var tx = (ushort)Interlocked.Increment(ref _transactionId);

        var frame = new byte[7 + pdu.Length];
        BinaryPrimitives.WriteUInt16BigEndian(frame.AsSpan(0), tx);
        // Protocol identifier 0x0000 (bytes 2–3 are zero-initialized)
        var length = checked((ushort)(1 + pdu.Length));
        BinaryPrimitives.WriteUInt16BigEndian(frame.AsSpan(4), length);
        frame[6] = slaveId;
        pdu.AsSpan().CopyTo(frame.AsSpan(7));

        await _stream.WriteAsync(frame, ct).ConfigureAwait(false);
        await _stream.FlushAsync(ct).ConfigureAwait(false);

        var headerBuf = new byte[7];
        await ReadExactAsync(_stream, headerBuf, ct).ConfigureAwait(false);

        var responseTx = BinaryPrimitives.ReadUInt16BigEndian(headerBuf.AsSpan(0));
        if (responseTx != tx)
        {
            throw new ModbusException(
                $"Modbus TCP MBAP: transaction ID mismatch. Expected 0x{tx:X4}, received 0x{responseTx:X4} (MODBUS TCP/IP, Section 3.1.3).");
        }

        var responseProtocol = BinaryPrimitives.ReadUInt16BigEndian(headerBuf.AsSpan(2));
        if (responseProtocol != 0)
        {
            throw new ModbusException(
                $"Modbus TCP MBAP: protocol ID must be 0x0000, received 0x{responseProtocol:X4} (MODBUS TCP/IP, Section 3.1.3).");
        }

        var responseUnit = headerBuf[6];
        if (responseUnit != slaveId)
        {
            throw new ModbusException(
                $"Modbus TCP MBAP: unit ID mismatch. Sent 0x{slaveId:X2}, received 0x{responseUnit:X2} in response.");
        }

        var mbapLength = BinaryPrimitives.ReadUInt16BigEndian(headerBuf.AsSpan(4));
        if (mbapLength < 2)
        {
            throw new ModbusException("Modbus TCP MBAP: length field is too small to contain a unit ID and at least one PDU byte.");
        }

        var responsePduLength = checked((int)mbapLength) - 1;
        if (responsePduLength < 1)
            throw new ModbusException("Modbus TCP: invalid response PDU length after MBAP (must be at least 1).");

        if (responsePduLength > 253)
        {
            throw new ModbusException(
                $"Modbus TCP: invalid response PDU length: {responsePduLength} (max 253 per Modbus Application Protocol V1.1b3, Section 4.3)");
        }

        var responsePdu = new byte[responsePduLength];
        await ReadExactAsync(_stream, responsePdu, ct).ConfigureAwait(false);
        return responsePdu;
    }

    private static async Task ReadExactAsync(NetworkStream stream, byte[] buffer, CancellationToken ct)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(offset, buffer.Length - offset), ct)
                .ConfigureAwait(false);
            if (read == 0)
                throw new ModbusException("Connection closed by remote host");
            offset += read;
        }
    }
}
