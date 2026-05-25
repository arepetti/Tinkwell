using System.IO.Pipes;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Tinkwell.Pipes;

/// <summary>
/// Base class for named pipe clients that use line-oriented (JSONL)
/// communication. Each call to <see cref="SendLineAsync"/> opens a
/// fresh connection, sends one line, reads one response line, and closes.
/// </summary>
/// <remarks>
/// Derive from this class and add protocol-specific methods (JSON
/// deserialization, typed commands, etc.) in the subclass.
/// </remarks>
public abstract class PipeClient
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    private readonly string _pipeName;
    private readonly int _timeoutMs;

    /// <summary>
    /// Logger available to derived classes.
    /// </summary>
    protected ILogger Logger { get; }

    /// <summary>
    /// Initializes the client with the target pipe name, a logger, and
    /// an optional timeout that covers the entire connect+write+read cycle.
    /// </summary>
    protected PipeClient(string pipeName, ILogger logger, int timeoutMs = 10_000)
    {
        _pipeName = pipeName;
        Logger = logger;
        _timeoutMs = timeoutMs;
    }

    /// <summary>
    /// Opens a connection to the named pipe, writes <paramref name="line"/>,
    /// reads the single-line response, and returns it.
    /// Returns <see langword="null"/> if the server closed the connection
    /// without sending a response.
    /// </summary>
    protected async Task<string?> SendLineAsync(string line, CancellationToken cancellationToken = default)
    {
        await using var pipe = new NamedPipeClientStream(
            ".", _pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(_timeoutMs);
        var token = timeoutCts.Token;

        await pipe.ConnectAsync(token);

        await using var writer = new StreamWriter(pipe, Utf8NoBom, leaveOpen: true)
        {
            AutoFlush = true,
            NewLine = "\n"
        };
        using var reader = new StreamReader(pipe, Utf8NoBom, leaveOpen: true);

        await writer.WriteLineAsync(line.AsMemory(), token);
        return await reader.ReadLineAsync(token);
    }
}
