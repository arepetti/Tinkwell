namespace Tinkwell.Actions.Executor;

internal interface IIntentDispatcher
{
    Task DispatchAsync(Intent intent, CancellationToken cancellationToken);
}