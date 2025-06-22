using System.IO.Pipes;
using System.Text;

namespace Tinkwell.Bootstrapper.Ipc;

public sealed class NamedPipeClient : INamedPipeClient
{
    public StreamReader Reader
    {
        get
        {
            if (_reader is null)
                throw new InvalidOperationException("Reader is not initialized. Call Connect() first.");

            return _reader;
        }
    }

    public StreamWriter Writer
    {
        get
        {
            if (_writer is null)
                throw new InvalidOperationException("Writer is not initialized. Call Connect() first.");

            return _writer;
        }
    }

    public void Connect(string serverName, string pipeName)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(NamedPipeClient));

        if (_client is not null)
            return;

        _client = new NamedPipeClientStream(serverName, pipeName, PipeDirection.InOut);
        _client.Connect();

        _writer = new StreamWriter(_client, Encoding.UTF8);
        _reader = new StreamReader(_client, Encoding.UTF8);
        _writer.AutoFlush = true;
    }

    public void Connect(string pipeName)
        => Connect(".", pipeName);

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

    public void SendCommand(string command)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(NamedPipeClient));

        if (_client is null || !_client.IsConnected)
            throw new InvalidOperationException("Client is not connected. Call Connect() first.");

        _writer!.WriteLine(command);
    }

    public Task SendCommandAsync(string command, CancellationToken cancellationToken = default)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(NamedPipeClient));

        if (_client is null || !_client.IsConnected)
            throw new InvalidOperationException("Client is not connected. Call Connect() first.");

        return _writer!.WriteLineAsync(command.AsMemory(), cancellationToken);
    }

    public string? SendCommandAndWaitReply(string command)
    {
        SendCommand(command);
        return _reader!.ReadLine();
    }

    public async Task<string?> SendCommandAndWaitReplyAsync(string command, CancellationToken cancellationToken = default)
    {
        await SendCommandAsync(command, cancellationToken);
        return await _reader!.ReadLineAsync(cancellationToken);
    }

    public async Task<T> SendCommandAndWaitReplyAsync<T>(string command, CancellationToken cancellationToken = default)
    {
        await SendCommandAsync(command, cancellationToken);

        string? resultAsText = await _reader!.ReadLineAsync(cancellationToken);
        if (resultAsText is null)
            throw new InvalidOperationException("Received null reply from the server.");

        T? result = System.Text.Json.JsonSerializer.Deserialize<T>(resultAsText);
        if (result is null)
            throw new InvalidOperationException($"Failed to deserialize reply to type {typeof(T).FullName}.");

        return result;
    }

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
            {
                _writer?.Dispose();
                _reader?.Dispose();
                _client?.Dispose();
            }

        }
        finally
        {
            _disposed = true;
        }
    }
}