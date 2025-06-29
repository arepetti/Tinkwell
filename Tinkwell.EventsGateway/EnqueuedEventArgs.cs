namespace Tinkwell.EventsGateway;

sealed class EnqueuedEventArgs : EventArgs
{
    public EnqueuedEventArgs(EventData data)
    {
        Data = data;
    }

    public EventData Data { get; }
}
