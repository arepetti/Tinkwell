using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Tinkwell.Runner;
using Tinkwell.Runlet.Events.Grpc.V1;

namespace Tinkwell.Runlet.Events;

/// <summary>
/// gRPC runlet hosting the generic event bus service. Runs in its own
/// runner so it is available before producers (signals, measures) start.
/// </summary>
public sealed class EventsRunlet : IGrpcRunlet
{
    public void ConfigureServices(IServiceCollection services, IConfiguration settings)
    {
        var subscriberCapacity = 1000;
        if (int.TryParse(settings["subscriber-channel-capacity"], out var cap) && cap >= 1)
        {
            subscriberCapacity = cap;
        }
        var fullMode = settings["subscriber-channel-full-mode"] is { } fm
            && System.Enum.TryParse<System.Threading.Channels.BoundedChannelFullMode>(fm, true, out var mode)
                ? mode : System.Threading.Channels.BoundedChannelFullMode.DropWrite;
        services.AddSingleton(new EventFanOutConfig(new ChannelConfig(subscriberCapacity, fullMode)));
        services.AddSingleton<EventFanOut>();
    }

    public void MapGrpcServices(IServiceCollection services)
    {
        services.AddSingleton<EventBusGrpcService>();
    }

    public void MapGrpcEndpoints(IGrpcEndpointMapper mapper)
    {
        mapper.MapService<EventBusGrpcService>(opts =>
        {
            opts.FriendlyName = "Events";
            opts.FamilyName = "events";
        });
    }
}

internal sealed record EventFanOutConfig(ChannelConfig SubscriberChannelConfig);
