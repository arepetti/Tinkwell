using System.Net.Sockets;
using System.Text;

namespace Tinkwell.Runlet.TextQuery.Transports;

internal sealed class TcpTextTransport : ITextTransport
{
    private readonly string _host;
    private readonly int _port;
    private TcpClient? _tcp;
    private NetworkStream? _stream;

    public TcpTextTransport(string host, int port)
    {
        _host = host;
        _port = port;
    }

    public async Task ConnectAsync(CancellationToken ct)
    {
        _tcp = new TcpClient();
        await _tcp.ConnectAsync(_host, _port, ct);
        _stream = _tcp.GetStream();
    }

    public async Task<string> QueryAsync(string? command, string lineTerminator, int timeoutMs, CancellationToken ct)
    {
        if (_stream is null)
            throw new InvalidOperationException("Not connected");

        if (command is not null)
        {
            var bytes = Encoding.ASCII.GetBytes(command + lineTerminator);
            await _stream.WriteAsync(bytes, ct);
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeoutMs);

        var buffer = new byte[4096];
        var sb = new StringBuilder();

        while (true)
        {
            int read;
            try
            {
                read = await _stream.ReadAsync(buffer, cts.Token);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                break;
            }

            if (read == 0)
            {
                break;
            }

            sb.Append(Encoding.ASCII.GetString(buffer, 0, read));

            if (sb.ToString().Contains(lineTerminator, StringComparison.Ordinal))
            {
                break;
            }

            if (sb.Length >= 65536)
            {
                break;
            }
        }

        return sb.ToString().Trim();
    }

    public async ValueTask DisposeAsync()
    {
        if (_stream is not null)
            await _stream.DisposeAsync();
        _tcp?.Dispose();
    }
}
