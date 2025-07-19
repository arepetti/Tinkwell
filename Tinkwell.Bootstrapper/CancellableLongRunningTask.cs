using System.Diagnostics.CodeAnalysis;

namespace Tinkwell;

/// <summary>
/// Provides a mechanism to run a long-running task that can be cancelled and awaited.
/// </summary>
public sealed class CancellableLongRunningTask
{
    /// <summary>
    /// Gets a value indicating whether the worker task is currently running.
    /// </summary>
    [MemberNotNullWhen(true, nameof(_worker))]
    public bool IsRunning
        => _worker is not null && !_worker.IsCompleted;

    /// <summary>
    /// Starts a long-running task using the specified <see cref="Action{CancellationToken}"/>.
    /// </summary>
    /// <param fileName="task">The action to execute, which receives a <see cref="CancellationToken"/>.</param>
    /// <param fileName="cancellationToken">An optional cancellation token to observe while waiting for the task to start.</param>
    /// <returns>A completed <see cref="Task"/> representing the start operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the worker task is already running.</exception>
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
        }, _cts.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default); // Pass CancellationToken to StartNew
        return Task.CompletedTask;
    }

    /// <summary>
    /// Starts a long-running asynchronous task using the specified <see cref="Func{CancellationToken, Task}"/>.
    /// </summary>
    /// <param fileName="task">The asynchronous function to execute, which receives a <see cref="CancellationToken"/>.</param>
    /// <param fileName="cancellationToken">An optional cancellation token to observe while waiting for the task to start.</param>
    /// <returns>A completed <see cref="Task"/> representing the start operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the worker task is already running.</exception>
    public Task StartAsync(Func<CancellationToken, Task> task, CancellationToken cancellationToken = default)
    {
        if (IsRunning)
            throw new InvalidOperationException("Worker task is already running.");

        _cts = new CancellationTokenSource();
        _worker = Task.Run(async () => // Use Task.Run for async delegates
        {
            try
            {
                await task(_cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        }, _cts.Token); // Pass CancellationToken to Task.Run
        return Task.CompletedTask;
    }

    /// <summary>
    /// Requests cancellation of the running task and waits for it to complete.
    /// </summary>
    /// <param fileName="cancellationToken">A cancellation token to observe while waiting for the task to stop.</param>
    /// <returns>A <see cref="Task"/> that represents the asynchronous stop operation.</returns>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (IsRunning)
        {
            _cts!.Cancel();
            try
            {
                await _worker.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // This exception is expected if the external cancellationToken is cancelled
                // or if the worker task itself was cancelled.
            }
            finally
            {
                _worker = null;
            }
        }
    }

    private CancellationTokenSource? _cts;
    private Task? _worker;
}
