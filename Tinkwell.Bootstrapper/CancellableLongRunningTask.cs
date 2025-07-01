using System.Diagnostics.CodeAnalysis;

namespace Tinkwell.Bootstrapper;

public sealed class CancellableLongRunningTask
{
    [MemberNotNullWhen(true, nameof(_worker))]
    public bool IsRunning
        => _worker is not null && !_worker.IsCompleted;

    public Task StartAsync(Action<CancellationToken> task, CancellationToken cancellationToken = default)
    {
        if (IsRunning)
            throw new InvalidOperationException("Worker task is already running.");

        _cts = new CancellationTokenSource();
        _worker = Task.Factory.StartNew(() =>
        {
            try
            {
                task(_cts.Token);
            }
            catch (OperationCanceledException)
            {
            }
        }, TaskCreationOptions.LongRunning);
        return Task.CompletedTask;
    }

    public Task StartAsync(Func<CancellationToken, Task> task, CancellationToken cancellationToken = default)
    {
        if (IsRunning)
            throw new InvalidOperationException("Worker task is already running.");

        _cts = new CancellationTokenSource();
        _worker = Task.Factory.StartNew(async () =>
        {
            try
            {
                await task(_cts.Token);
            }
            catch (OperationCanceledException)
            {
            }
        }, TaskCreationOptions.LongRunning);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (IsRunning)
        {
            _cts!.Cancel();
            await _worker.WaitAsync(cancellationToken);
            _worker = null;
        }
    }

    private CancellationTokenSource? _cts;
    private Task? _worker;
}