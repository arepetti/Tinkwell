using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Tinkwell.Bootstrapper.Ipc;

public sealed class NamedPipeServer : INamedPipeServer
{
    public static readonly int DefaultMaxConcurrentConnections = 4;

    public int MaxConcurrentConnections { get; set; } = DefaultMaxConcurrentConnections;

    public event EventHandler? Connected;
    public event EventHandler? Disconnected;
    public event EventHandler<NamedPipeServerProcessEventArgs>? Process;

    public void Open(string pipeName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pipeName);

        if (IsOpen)
            throw new InvalidOperationException("The pipe server is already open.");

        _pipeName = pipeName;
        _abortTokenSource = new CancellationTokenSource();

        CreatePipe();
    }

    public void Close()
    {
        if (!IsOpen)
            return;

        _abortTokenSource.Cancel();

        foreach (var pipe in _pipes.ToArray())
        {
            try
            {
                pipe.Stop();
            }
            catch (IOException)
            {
                // Ignore IO exceptions that may occur if the pipe is already closed.
            }
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

        pipe.Process += (_, args) => Process?.Invoke(this, args);
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
