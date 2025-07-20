using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Tinkwell.Bootstrapper.Ipc;

/// <summary>
/// Server implementation for a named pipe server which accepts
/// multiple simultaneous connections.
/// </summary>
public sealed class NamedPipeServer : INamedPipeServer
{
    /// <summary>
    /// Default number of concurrent connections.
    /// </summary>
    public static readonly int DefaultMaxConcurrentConnections = 4;

    /// <inheritsdocs />
    public int MaxConcurrentConnections { get; set; } = DefaultMaxConcurrentConnections;

    /// <inheritsdocs />
    public event EventHandler? Connected;

    /// <inheritsdocs />
    public event EventHandler? Disconnected;

    /// <inheritsdocs />
    public ProcessPipeDataDelegate? ProcessAsync {  get; set; }

    /// <inheritsdocs />
    public void Open(string pipeName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pipeName);

        if (IsOpen)
            throw new InvalidOperationException("The pipe server is already open.");

        _pipeName = pipeName;
        _abortTokenSource = new CancellationTokenSource();

        CreatePipe();
    }

    /// <inheritsdocs />
    public void Close()
    {
        if (!IsOpen)
            return;

        _abortTokenSource.Cancel();

        var stopTasks = new List<Task>();
        foreach (var pipe in _pipes.ToArray())
        {
            try
            {
                stopTasks.Add(pipe.StopAsync());
            }
            catch (IOException)
            {
                // Ignore IO exceptions that may occur if the pipe is already closed.
            }
        }

        // Synchronously wait for all stop tasks to complete.
        // This is necessary because Close() is a synchronous method.
        try
        {
            Task.WhenAll(stopTasks).Wait(TimeSpan.FromSeconds(5));
        }
        catch (AggregateException ex)
        {
            // Log the exception, but don't rethrow, as Close() is synchronous.
            // This indicates that some pipes did not stop gracefully.
            Debug.WriteLine($"Error stopping pipes: {ex.Message}");
        }

        _pipes.Clear();
        _abortTokenSource.Dispose();
        _abortTokenSource = null;
    }

    private string? _pipeName;
    private CancellationTokenSource? _abortTokenSource;
    private readonly List<Pipe> _pipes = [];

    [MemberNotNullWhen(true, nameof(_abortTokenSource))]
    private bool IsOpen => _abortTokenSource is not null;

    private void CreatePipe()
    {
        Debug.Assert(!string.IsNullOrWhiteSpace(_pipeName));
        Debug.Assert(_abortTokenSource is not null);

        var pipe = new Pipe(_pipeName, MaxConcurrentConnections, _abortTokenSource);

        pipe.ProcessAsync = ProcessAsync;
        pipe.Connected += (_, _) =>
        {
            Connected?.Invoke(this, EventArgs.Empty);
            CreatePipe();
        };
        pipe.Disconnected += (sender, _) =>
        {
            Disconnected?.Invoke(this, EventArgs.Empty);
            lock (_pipes)
                _pipes.Remove((Pipe)sender!);
        };

        lock (_pipes)
            _pipes.Add(pipe);

        pipe.Start();
    }
}
