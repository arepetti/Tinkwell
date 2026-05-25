namespace Tinkwell.Modbus;

/// <summary>
/// Modbus TCP client without an internal I/O lock. Same MBAP, connection, and validation behavior
/// as <see cref="ModbusTcpClient"/>, but concurrent calls may interleave reads and writes on the
/// underlying <see cref="System.Net.Sockets.NetworkStream"/>, which is not safe unless you
/// guarantee one operation at a time.
/// </summary>
/// <remarks>
/// <para>Prefer <see cref="ModbusTcpClient"/> for general use, where multiple awaiters (or calls
/// from a thread pool) may hit the same instance.</para>
/// <para>Typical use for this class: a dedicated single-threaded loop, a higher-level
/// <see cref="System.Threading.Channels"/> queue, or one client per logical session.</para>
/// </remarks>
/// <example>
/// <para>Enforce a single in-flight request: one consumer reads from a channel and calls this client, so the TCP stream is not used concurrently without <see cref="ModbusTcpClient"/>'s lock.</para>
/// <code language="csharp">
/// await using var client = new UnsynchronizedModbusTcpClient("192.168.1.20");
/// await client.ConnectAsync();
/// var work = System.Threading.Channels.Channel.CreateUnbounded&lt;(byte slave, ushort start, ushort count)&gt;();
/// await work.Writer.WriteAsync((1, 0x0100, 2));
/// await foreach (var op in work.Reader.ReadAllAsync())
///     _ = await client.ReadHoldingRegistersAsync(op.slave, op.start, op.count);
/// </code>
/// </example>
public sealed class UnsynchronizedModbusTcpClient : ModbusTcpClientBase
{
    /// <summary>
    /// Initializes a new unsynchronized Modbus TCP client.
    /// </summary>
    /// <param name="host">Hostname or IP address of the Modbus TCP device or gateway.</param>
    /// <param name="port">TCP port (default 502 per MODBUS TCP/IP Implementation Guide, Section 4.1).</param>
    public UnsynchronizedModbusTcpClient(string host, int port = 502)
        : base(host, port)
    {
    }
}
