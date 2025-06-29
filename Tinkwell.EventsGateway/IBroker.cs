using Microsoft.Extensions.Hosting;

namespace Tinkwell.EventsGateway;

interface IBroker : IHostedService
{
    public event EventHandler<EnqueuedEventArgs>? Enqueued;

    void Publish(EventData eventData);
}