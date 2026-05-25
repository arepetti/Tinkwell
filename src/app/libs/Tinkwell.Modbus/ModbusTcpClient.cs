namespace Tinkwell.Modbus;

/// <summary>
/// Modbus TCP client with one-at-a-time request serialization. Wraps a Modbus PDU in a MBAP
/// header and uses a <see cref="SemaphoreSlim"/> with a count of 1 so concurrent public API
/// calls do not interleave on the same TCP stream.
/// </summary>
/// <remarks>
/// <para>Use <see cref="UnsynchronizedModbusTcpClient"/> if you need maximum throughput and can
/// guarantee exclusive access, or you serialize requests at a higher level.</para>
/// </remarks>
/// <example>
/// <para>Connect over Modbus TCP, read a holding register, and interpret it as a signed 16-bit value.</para>
/// <code language="csharp">
/// await using var client = new ModbusTcpClient("192.168.1.100");
/// await client.ConnectAsync();
/// var regs = await client.ReadHoldingRegistersAsync(1, 0x0100, 1);
/// short temperature = RegisterDecoder.ToInt16(regs[0]);
/// </code>
/// </example>
public sealed class ModbusTcpClient : ModbusTcpClientBase
{
    private readonly SemaphoreSlim _ioGate = new(1, 1);

    /// <summary>
    /// Initializes a new synchronized Modbus TCP client.
    /// </summary>
    /// <param name="host">Hostname or IP address of the Modbus TCP device or gateway.</param>
    /// <param name="port">TCP port (default 502 per MODBUS TCP/IP Implementation Guide, Section 4.1).</param>
    public ModbusTcpClient(string host, int port = 502)
        : base(host, port)
    {
    }

    /// <inheritdoc />
    /// <remarks>
    /// Concurrent callers are serialized through a binary semaphore so only one MBAP
    /// request/response pair is in flight at a time on the underlying TCP stream.
    /// </remarks>
    protected override async Task<byte[]> SendRequestAsync(
        byte slaveId,
        byte[] pdu,
        CancellationToken ct)
    {
        ThrowIfDisposed();
        await _ioGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            return await base.SendRequestAsync(slaveId, pdu, ct).ConfigureAwait(false);
        }
        finally
        {
            _ioGate.Release();
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>The semaphore is intentionally not disposed: <see cref="SemaphoreSlim"/> without
    /// an <see cref="SemaphoreSlim.AvailableWaitHandle"/> access holds no OS handles, and
    /// explicit disposal while a concurrent caller is inside <c>WaitAsync</c> would throw
    /// <see cref="ObjectDisposedException"/>. Letting the GC collect it is safe and avoids
    /// the dispose-while-waiting race.</para>
    /// </remarks>
    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync().ConfigureAwait(false);
    }
}
