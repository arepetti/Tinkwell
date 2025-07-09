namespace Tinkwell.Actions.Executor.Agents;

[Agent("pass")]
public sealed class PassAgent : IAgent
{
    public object? Settings { get; } = null;

    public Task ExecuteAsync(CancellationToken cancellationToken)
        => Task.CompletedTask;
}

