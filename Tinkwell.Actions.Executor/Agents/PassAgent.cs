namespace Tinkwell.Actions.Executor.Agents;

[Agent("pass")]
sealed class PassAgent : IAgent
{
    public object? Settings { get; } = null;

    public Task ExecuteAsync(CancellationToken cancellationToken)
        => Task.CompletedTask;
}

