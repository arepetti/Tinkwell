using System.Diagnostics;
using System.IO.Pipes;
using Grpc.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Tinkwell.Orchestrator.Services;

public sealed class CommandServerClient : ICommandServerClient, IDisposable
{
    public CommandServerClient(ILogger<OrchestratorService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    public async Task<string> SendCommandAsync(string command)
    {
        await ConnectAsync();

        Debug.Assert(_reader is not null);
        Debug.Assert(_writer is not null);

        _logger.LogDebug("Sending command {Command}", command);
        await _writer.WriteLineAsync(command);
        var response = await _reader.ReadLineAsync() ?? "";

        return response;
    }

    private readonly ILogger<OrchestratorService> _logger;
    private readonly IConfiguration _configuration;
    private NamedPipeClientStream? _client;
    private StreamReader? _reader;
    private StreamWriter? _writer;
    private bool _disposed;

    private async Task ConnectAsync()
    {
        if (_client is not null && _client.IsConnected)
            return;

        string? pipeId = _configuration["OrchestratorCsId"];
        if (pipeId is null)
        {
            _logger.LogError("Cannot connect to the Orchestrator Command Server: pipe ID isn't specified.");
            throw new RpcException(new Status(StatusCode.Internal, "Configuration error"));
        }

        // This is the service exposing remote management, we can safely assume that the named pipe
        // is on the same machine.
        _logger.LogDebug("Connecting to Command Server pipe: {ID}", pipeId);
        _client = new NamedPipeClientStream(".", pipeId, PipeDirection.InOut);
        await _client.ConnectAsync();

        _reader = new StreamReader(_client);
        _writer = new StreamWriter(_client) { AutoFlush = true };
    }

    private void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        try
        {
            if (disposing)
            {
                _reader?.Dispose();
                _writer?.Dispose();
                _client?.Dispose();
            }
        }
        finally
        {
            _disposed = true;
        }
    }
}
