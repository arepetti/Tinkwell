using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Tinkwell.Runlet.TextQuery.Tests;

/// <summary>Single-connection loopback server for <see cref="TcpTextTransport" /> tests.</summary>
internal sealed class TcpTextTransportTestServer : IAsyncDisposable
{
    private readonly TcpListener _listener;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _task;

    public int Port { get; }

    public TcpTextTransportTestServer(Func<NetworkStream, CancellationToken, Task> onConnected)
    {
        _listener = new TcpListener(IPAddress.Loopback, 0);
        _listener.Start();
        Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        _task = ConnectOnceAsync(onConnected);
    }

    private async Task ConnectOnceAsync(Func<NetworkStream, CancellationToken, Task> onConnected)
    {
        try
        {
            using var client = await _listener.AcceptTcpClientAsync(_cts.Token);
            var stream = client.GetStream();
            await onConnected(stream, _cts.Token);
        }
        catch (OperationCanceledException) when (_cts.IsCancellationRequested)
        {
        }
    }

    public static async Task SendInChunksAsync(
        NetworkStream stream,
        ReadOnlyMemory<byte> data,
        int chunkSize,
        int interChunkDelayMs,
        CancellationToken cancellationToken = default)
    {
        for (int i = 0; i < data.Length; i += chunkSize)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var n = Math.Min(chunkSize, data.Length - i);
            await stream.WriteAsync(data[i..(i + n)], cancellationToken);
            if (interChunkDelayMs > 0)
                await Task.Delay(interChunkDelayMs, cancellationToken);
        }

        await stream.FlushAsync(cancellationToken);
    }

    public static byte[] BuildAsciiString(int length) => Encoding.ASCII.GetBytes(new string('A', length));

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        try
        {
            _listener.Stop();
        }
        catch
        {
            // ignore
        }
        try
        {
            await _task.ConfigureAwait(false);
        }
        catch
        {
            // best-effort
        }
        _cts.Dispose();
    }
}
