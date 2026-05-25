using System.IO.Pipes;
using System.Text;

namespace Tinkwell.Pipes;

/// <summary>
/// A single named pipe connection. Provides line-oriented (JSONL)
/// reading and writing over a connected <see cref="NamedPipeServerStream"/>.
/// </summary>
/// <remarks>
/// Each connection is single-use: once disposed or disconnected,
/// it cannot be reused. The server creates a fresh listener
/// for the next client.
/// </remarks>
public sealed class PipeConnection : IAsyncDisposable
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    private readonly NamedPipeServerStream _stream;
    private readonly StreamReader _reader;
    private readonly StreamWriter _writer;
    private bool _disposed;

    /// <summary>
    /// A unique ID for this connection, useful for logging.
    /// </summary>
    public Guid Id { get; } = Guid.NewGuid();

    /// <summary>
    /// Whether the underlying pipe is still connected.
    /// </summary>
    public bool IsConnected => !_disposed && _stream.IsConnected;

    /// <summary>Wraps <paramref name="stream"/> for line-oriented I/O.</summary>
    public PipeConnection(NamedPipeServerStream stream)
    {
        _stream = stream;
        _reader = new StreamReader(stream, Utf8NoBom, leaveOpen: true);
        _writer = new StreamWriter(stream, Utf8NoBom, leaveOpen: true)
        {
            AutoFlush = true,
            NewLine = "\n"
        };
    }

    /// <summary>
    /// Reads one line (JSONL message) from the client.
    /// Returns <see langword="null"/> when the client disconnects or the stream ends.
    /// </summary>
    public async Task<string?> ReadLineAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        try
        {
            return await _reader.ReadLineAsync(cancellationToken);
        }
        catch (IOException)
        {
            return null;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
    }

    /// <summary>
    /// Writes one line (JSONL message) to the client.
    /// </summary>
    public async Task WriteLineAsync(string line, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        await _writer.WriteLineAsync(line.AsMemory(), cancellationToken);
    }

    /// <summary>Disconnects and releases the underlying pipe stream.</summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;
        _disposed = true;

        try
        {
            if (_stream.IsConnected)
                _stream.Disconnect();
        }
        catch (OutOfMemoryException) { Environment.FailFast("Out of memory"); }
        catch (Exception)
        {
            // Best-effort disconnect
        }

        _reader.Dispose();
        await _writer.DisposeAsync();
        await _stream.DisposeAsync();
    }
}