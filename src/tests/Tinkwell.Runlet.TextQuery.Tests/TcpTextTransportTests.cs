using Tinkwell.Runlet.TextQuery.Transports;

namespace Tinkwell.Runlet.TextQuery.Tests;

public class TcpTextTransportTests
{
    [Fact]
    public async Task QueryAsync_SingleResponseWithLineTerminator_ReturnsContentBeforeTerm()
    {
        var payload = "measurement 1.25\n"u8.ToArray();
        await using var server = new TcpTextTransportTestServer(
            async (stream, _) =>
            {
                await stream.WriteAsync(payload);
            });

        await using var transport = new TcpTextTransport("127.0.0.1", server.Port);
        await transport.ConnectAsync(CancellationToken.None);

        var result = await transport.QueryAsync(
            command: null,
            lineTerminator: "\n",
            timeoutMs: 5_000,
            CancellationToken.None);

        Assert.Equal("measurement 1.25", result);
    }

    [Fact]
    public async Task QueryAsync_MultichunkResponse_CollectsUntilTerminator()
    {
        var data = "hello\n"u8.ToArray();
        await using var server = new TcpTextTransportTestServer(
            async (stream, ct) =>
            {
                await TcpTextTransportTestServer.SendInChunksAsync(
                    stream, data, chunkSize: 1, interChunkDelayMs: 2, ct);
            });

        await using var transport = new TcpTextTransport("127.0.0.1", server.Port);
        await transport.ConnectAsync(CancellationToken.None);

        var result = await transport.QueryAsync(null, "\n", 10_000, CancellationToken.None);

        Assert.Equal("hello", result);
    }

    [Fact]
    public async Task QueryAsync_ReadTimeout_ReturnsEmptyWhenNoData()
    {
        await using var server = new TcpTextTransportTestServer(
            static async (_, ct) => { await Task.Delay(60_000, ct); });

        await using var transport = new TcpTextTransport("127.0.0.1", server.Port);
        await transport.ConnectAsync(CancellationToken.None);

        var result = await transport.QueryAsync(
            null,
            "\n",
            timeoutMs: 100,
            CancellationToken.None);

        Assert.Equal("", result);
    }

    [Fact]
    public async Task QueryAsync_EmptyResponse_WhenServerClosesBeforeSend()
    {
        await using var server = new TcpTextTransportTestServer(
            static (stream, _) => stream.DisposeAsync().AsTask());

        await using var transport = new TcpTextTransport("127.0.0.1", server.Port);
        await transport.ConnectAsync(CancellationToken.None);

        var result = await transport.QueryAsync(
            null,
            "\n",
            timeoutMs: 5_000,
            CancellationToken.None);

        Assert.Equal("", result);
    }

    [Fact]
    public async Task QueryAsync_NoTerminator_StopsAtBufferCap()
    {
        const int overshoot = 70_000;
        await using var server = new TcpTextTransportTestServer(
            async (stream, ct) =>
            {
                var big = TcpTextTransportTestServer.BuildAsciiString(overshoot);
                await stream.WriteAsync(big, ct);
            });

        await using var transport = new TcpTextTransport("127.0.0.1", server.Port);
        await transport.ConnectAsync(CancellationToken.None);

        var result = await transport.QueryAsync(
            null,
            lineTerminator: "\n",
            timeoutMs: 10_000,
            CancellationToken.None);

        Assert.InRange(result.Length, 65_536, overshoot);
        Assert.DoesNotContain('\n', result);
    }

    [Fact]
    public async Task QueryAsync_NotConnected_Throws()
    {
        await using var transport = new TcpTextTransport("127.0.0.1", 1);

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await transport.QueryAsync(null, "\n", 1_000, CancellationToken.None);
        });
    }
}
