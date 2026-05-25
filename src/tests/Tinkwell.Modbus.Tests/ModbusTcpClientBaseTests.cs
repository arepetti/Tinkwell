using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;

namespace Tinkwell.Modbus.Tests;

public class ModbusTcpClientBaseTests
{
    [Fact]
    public async Task SendRequest_MbapTransactionIdMismatch_Throws()
    {
        await using var s = await LoopbackModbusServer.StartAsync();
        var server = RunTransactionMismatchAsync(s.ServerStream, s.Cts.Token);
        var ex = await Assert.ThrowsAsync<ModbusException>(
            () => s.Client.ReadHoldingRegistersAsync(1, 0, 1, s.Cts.Token));
        await server;
        Assert.Contains("transaction ID mismatch", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendRequest_MbapProtocolIdNonZero_Throws()
    {
        await using var s = await LoopbackModbusServer.StartAsync();
        var server = RunProtocolMismatchAsync(s.ServerStream, s.Cts.Token);
        var ex = await Assert.ThrowsAsync<ModbusException>(
            () => s.Client.ReadHoldingRegistersAsync(1, 0, 1, s.Cts.Token));
        await server;
        Assert.Contains("protocol ID", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendRequest_MbapUnitIdMismatch_Throws()
    {
        await using var s = await LoopbackModbusServer.StartAsync();
        var server = RunUnitMismatchAsync(s.ServerStream, s.Cts.Token);
        var ex = await Assert.ThrowsAsync<ModbusException>(
            () => s.Client.ReadHoldingRegistersAsync(1, 0, 1, s.Cts.Token));
        await server;
        Assert.Contains("unit ID mismatch", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendRequest_MbapLengthFieldTooSmall_Throws()
    {
        await using var s = await LoopbackModbusServer.StartAsync();
        var server = RunLengthTooSmallAsync(s.ServerStream, s.Cts.Token);
        var ex = await Assert.ThrowsAsync<ModbusException>(
            () => s.Client.ReadHoldingRegistersAsync(1, 0, 1, s.Cts.Token));
        await server;
        Assert.Contains("length field is too small", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendRequest_ResponsePduExceedsProtocolMax_Throws()
    {
        await using var s = await LoopbackModbusServer.StartAsync();
        var server = RunPduLengthTooLongAsync(s.ServerStream, s.Cts.Token);
        var ex = await Assert.ThrowsAsync<ModbusException>(
            () => s.Client.ReadHoldingRegistersAsync(1, 0, 1, s.Cts.Token));
        await server;
        Assert.Contains("max 253", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReadHoldingRegistersAsync_ValidMbapAndPdu_Decodes()
    {
        await using var s = await LoopbackModbusServer.StartAsync();
        var server = RunValidReadHoldingResponseAsync(s.ServerStream, s.Cts.Token);
        var regs = await s.Client.ReadHoldingRegistersAsync(1, 0, 1, s.Cts.Token);
        await server;
        Assert.Single(regs);
        Assert.Equal(0x1234, regs[0]);
    }

    private static async Task RunTransactionMismatchAsync(NetworkStream stream, CancellationToken ct)
    {
        var request = await ReadAduRequestAsync(stream, ct);
        var tx = BinaryPrimitives.ReadUInt16BigEndian(request.AsSpan(0));
        var responseHeader = new byte[7];
        BinaryPrimitives.WriteUInt16BigEndian(responseHeader.AsSpan(0), (ushort)(tx + 1));
        BinaryPrimitives.WriteUInt16BigEndian(responseHeader.AsSpan(2), 0);
        BinaryPrimitives.WriteUInt16BigEndian(responseHeader.AsSpan(4), 2);
        responseHeader[6] = 1;
        await stream.WriteAsync(responseHeader, ct);
    }

    private static async Task RunProtocolMismatchAsync(NetworkStream stream, CancellationToken ct)
    {
        var request = await ReadAduRequestAsync(stream, ct);
        var tx = BinaryPrimitives.ReadUInt16BigEndian(request.AsSpan(0));
        var responseHeader = new byte[7];
        BinaryPrimitives.WriteUInt16BigEndian(responseHeader.AsSpan(0), tx);
        BinaryPrimitives.WriteUInt16BigEndian(responseHeader.AsSpan(2), 0x0001);
        BinaryPrimitives.WriteUInt16BigEndian(responseHeader.AsSpan(4), 2);
        responseHeader[6] = 1;
        await stream.WriteAsync(responseHeader, ct);
    }

    private static async Task RunUnitMismatchAsync(NetworkStream stream, CancellationToken ct)
    {
        var request = await ReadAduRequestAsync(stream, ct);
        var tx = BinaryPrimitives.ReadUInt16BigEndian(request.AsSpan(0));
        var responseHeader = new byte[7];
        BinaryPrimitives.WriteUInt16BigEndian(responseHeader.AsSpan(0), tx);
        BinaryPrimitives.WriteUInt16BigEndian(responseHeader.AsSpan(2), 0);
        BinaryPrimitives.WriteUInt16BigEndian(responseHeader.AsSpan(4), 2);
        responseHeader[6] = 0xF7;
        await stream.WriteAsync(responseHeader, ct);
    }

    private static async Task RunLengthTooSmallAsync(NetworkStream stream, CancellationToken ct)
    {
        var request = await ReadAduRequestAsync(stream, ct);
        var tx = BinaryPrimitives.ReadUInt16BigEndian(request.AsSpan(0));
        var responseHeader = new byte[7];
        BinaryPrimitives.WriteUInt16BigEndian(responseHeader.AsSpan(0), tx);
        BinaryPrimitives.WriteUInt16BigEndian(responseHeader.AsSpan(2), 0);
        BinaryPrimitives.WriteUInt16BigEndian(responseHeader.AsSpan(4), 1);
        responseHeader[6] = 1;
        await stream.WriteAsync(responseHeader, ct);
    }

    private static async Task RunPduLengthTooLongAsync(NetworkStream stream, CancellationToken ct)
    {
        var request = await ReadAduRequestAsync(stream, ct);
        var tx = BinaryPrimitives.ReadUInt16BigEndian(request.AsSpan(0));
        const int pdu = 254;
        var responseHeader = new byte[7];
        BinaryPrimitives.WriteUInt16BigEndian(responseHeader.AsSpan(0), tx);
        BinaryPrimitives.WriteUInt16BigEndian(responseHeader.AsSpan(2), 0);
        BinaryPrimitives.WriteUInt16BigEndian(responseHeader.AsSpan(4), (ushort)(1 + pdu));
        responseHeader[6] = 1;
        await stream.WriteAsync(responseHeader, ct);
        await stream.WriteAsync(new byte[pdu], ct);
    }

    private static async Task RunValidReadHoldingResponseAsync(NetworkStream stream, CancellationToken ct)
    {
        var request = await ReadAduRequestAsync(stream, ct);
        var tx = BinaryPrimitives.ReadUInt16BigEndian(request.AsSpan(0));
        var unit = request[6];
        var pdu = new byte[] { 0x03, 0x02, 0x12, 0x34 };
        var mbap = BuildResponseMbap(tx, unit, pdu);
        await stream.WriteAsync(mbap, ct);
        await stream.WriteAsync(pdu, ct);
    }

    private static byte[] BuildResponseMbap(ushort transactionId, byte unitId, byte[] pdu)
    {
        var length = (ushort)(1 + pdu.Length);
        var h = new byte[7];
        BinaryPrimitives.WriteUInt16BigEndian(h.AsSpan(0), transactionId);
        BinaryPrimitives.WriteUInt16BigEndian(h.AsSpan(2), 0);
        BinaryPrimitives.WriteUInt16BigEndian(h.AsSpan(4), length);
        h[6] = unitId;
        return h;
    }

    private static async Task<byte[]> ReadAduRequestAsync(NetworkStream stream, CancellationToken ct)
    {
        var first = new byte[6];
        await ReadExactAsync(stream, first, ct);
        var moreLen = BinaryPrimitives.ReadUInt16BigEndian(first.AsSpan(4));
        var rest = new byte[moreLen];
        await ReadExactAsync(stream, rest, ct);
        var all = new byte[first.Length + rest.Length];
        first.AsSpan().CopyTo(all);
        rest.AsSpan().CopyTo(all.AsSpan(6));
        return all;
    }

    private static async Task ReadExactAsync(NetworkStream stream, byte[] buffer, CancellationToken ct)
    {
        var o = 0;
        while (o < buffer.Length)
        {
            var n = await stream.ReadAsync(buffer.AsMemory(o, buffer.Length - o), ct);
            if (n == 0)
            {
                throw new IOException("Client closed the connection");
            }

            o += n;
        }
    }

    private sealed class LoopbackModbusServer : IAsyncDisposable
    {
        private readonly TcpClient _accepted;

        public required UnsynchronizedModbusTcpClient Client { get; init; }
        public required NetworkStream ServerStream { get; init; }
        public required TcpListener Listener { get; init; }
        public required CancellationTokenSource Cts { get; init; }

        private LoopbackModbusServer(TcpClient accepted) => _accepted = accepted;

        public static async Task<LoopbackModbusServer> StartAsync()
        {
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var localEp = (IPEndPoint)listener.LocalEndpoint;
            var client = new UnsynchronizedModbusTcpClient(localEp.Address.ToString(), localEp.Port);
            var accept = listener.AcceptTcpClientAsync();
            await client.ConnectAsync(cts.Token);
            var accepted = await accept;
            return new LoopbackModbusServer(accepted)
            {
                Client = client,
                ServerStream = accepted.GetStream(),
                Listener = listener,
                Cts = cts,
            };
        }

        public async ValueTask DisposeAsync()
        {
            Cts.Dispose();
            await Client.DisposeAsync();
            await ServerStream.DisposeAsync();
            _accepted.Dispose();
            Listener.Stop();
        }
    }
}
