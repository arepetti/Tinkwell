using System.Diagnostics.CodeAnalysis;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;

namespace Tinkwell.Bootstrapper.Ipc;

/// <summary>
/// Implements a named pipe client for inter-process communication.
/// </summary>
public sealed class NamedPipeClient : INamedPipeClient
{
    /// <summary>
    /// Gets a value indicating whether the client is connected.
    /// </summary>
    [MemberNotNullWhen(true, nameof(_client), nameof(_reader), nameof(_writer))]
    public bool IsConnected
        => _client is not null && _client.IsConnected;

    /// <summary>
    /// Gets the stream reader for the pipe.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if not connected.</exception>
    public StreamReader Reader
    {
        get
        {
            if (!IsConnected)
                throw new InvalidOperationException("Reader is not initialized. Call Connect() first.");

            return _reader;
        }
    }

    /// <summary>
    /// Gets the stream writer for the pipe.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if not connected.</exception>
    public StreamWriter Writer
    {
        get
        {
            if (!IsConnected)
                throw new InvalidOperationException("Writer is not initialized. Call Connect() first.");

            return _writer;
        }
    }

    /// <summary>
    /// Connects to the specified server and pipe.
    /// </summary>
    /// <param name="serverName">The server name.</param>
    /// <param name="pipeName">The pipe name.</param>
    public void Connect(string serverName, string pipeName)
    {
        Console.WriteLine("Connecting for real");
        if (_disposed)
            throw new ObjectDisposedException(nameof(NamedPipeClient));

        Console.WriteLine("Check I'm not connected already");
        if (IsConnected)
            return;

        Console.WriteLine("Building the stream");
        _client = new NamedPipeClientStream(serverName, pipeName, PipeDirection.InOut);
        _client.Connect();

        _writer = new StreamWriter(_client, Encoding.UTF8);
        _reader = new StreamReader(_client, Encoding.UTF8);
        _writer.AutoFlush = true;
        Console.WriteLine($"Finished {IsConnected}");
    }

    /// <summary>
    /// Asynchronously connects to the specified server and pipe.
    /// </summary>
    /// <param name="serverName">The server name.</param>
    /// <param name="pipeName">The pipe name.</param>
    /// <param name="timeout">The connection timeout.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    public async Task ConnectAsync(string serverName, string pipeName, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(NamedPipeClient));

        if (IsConnected)
            return;

        _client = new NamedPipeClientStream(serverName, pipeName, PipeDirection.InOut);
        await _client.ConnectAsync(timeout, cancellationToken);

        _writer = new StreamWriter(_client, Encoding.UTF8);
        _reader = new StreamReader(_client, Encoding.UTF8);
        _writer.AutoFlush = true;
    }

    /// <summary>
    /// Connects to the specified pipe on the local server.
    /// </summary>
    /// <param name="pipeName">The pipe name.</param>
    public void Connect(string pipeName)
        => Connect(".", pipeName);

    /// <summary>
    /// Disconnects the client from the pipe.
    /// </summary>
    public void Disconnect()
    {
        if (_client is null || _disposed)
            return;

        try
        {
            _writer?.Flush();
        }
        catch (IOException)
        {
            // Ignore any IO exceptions during flush, as the pipe might be closed.
        }

        _writer?.Dispose();
        _reader?.Dispose();
        _client?.Dispose();

        _writer = null;
        _reader = null;
        _client = null;
    }

    /// <summary>
    /// Sends a command to the server.
    /// </summary>
    /// <param name="command">The command to send.</param>
    public void SendCommand(string command)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(NamedPipeClient));

        if (!IsConnected)
            throw new InvalidOperationException("Client is not connected. Call Connect() first.");

        _writer!.WriteLine(command);
    }

    /// <summary>
    /// Asynchronously sends a command to the server.
    /// </summary>
    /// <param name="command">The command to send.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    public Task SendCommandAsync(string command, CancellationToken cancellationToken = default)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(NamedPipeClient));

        Console.WriteLine($"'{command} {_client} --- {_client?.IsConnected} -- {Environment.StackTrace}");
        if (!IsConnected)
            throw new InvalidOperationException("Client is not connected. Call Connect() first.");

        return _writer!.WriteLineAsync(command.AsMemory(), cancellationToken);
    }

    /// <summary>
    /// Sends a command and waits for a reply from the server.
    /// </summary>
    /// <param name="command">The command to send.</param>
    /// <returns>The reply as a string.</returns>
    public string? SendCommandAndWaitReply(string command)
    {
        SendCommand(command);
        return _reader!.ReadLine();
    }

    /// <summary>
    /// Asynchronously sends a command and waits for a reply from the server.
    /// </summary>
    /// <param name="command">The command to send.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The reply as a string.</returns>
    public async Task<string?> SendCommandAndWaitReplyAsync(string command, CancellationToken cancellationToken = default)
    {
        await SendCommandAsync(command, cancellationToken);
        return await _reader!.ReadLineAsync(cancellationToken);
    }

    /// <summary>
    /// Asynchronously sends a command and waits for a reply from the server, deserializing the reply to the specified type.
    /// </summary>
    /// <typeparam name="T">The type to deserialize the reply to.</typeparam>
    /// <param name="command">The command to send.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The reply deserialized to type <typeparamref name="T"/>.</returns>
    /// <exception cref="InvalidOperationException">Thrown if deserialization fails.</exception>
    public async Task<T> SendCommandAndWaitReplyAsync<T>(string command, CancellationToken cancellationToken = default)
    {
        await SendCommandAsync(command, cancellationToken);

        string? resultAsText = await _reader!.ReadLineAsync(cancellationToken);
        if (resultAsText is null)
            throw new InvalidOperationException("Received null reply from the server.");

        // JsonSerializer is a bit quirky, apparently it's OK to to consider JsonElement as a good
        // fit for object! For example if T is Dictionary<string, object> then values are going to be JsonElement.
        var options = new JsonSerializerOptions();
        options.Converters.Add(new ObjectJsonConverter());
        T? result = JsonSerializer.Deserialize<T>(resultAsText, options);
        if (result is null)
            throw new InvalidOperationException($"Failed to deserialize reply to type {typeof(T).FullName}.");

        return result;
    }

    /// <summary>
    /// Releases all resources used by the <see cref="NamedPipeClient"/>.
    /// </summary>
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    private NamedPipeClientStream? _client;
    private StreamReader? _reader;
    private StreamWriter? _writer;
    private bool _disposed;

    private void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        try
        {
            if (disposing)
                Disconnect();

        }
        finally
        {
            _disposed = true;
        }
    }
}
