using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Tinkwell.Actions.Abstractions;
using Tinkwell.Events;
using Tinkwell.Expressions;
using Tinkwell.Runner;
using Tinkwell.Runlet.Actions.Handlers;
using EventsGrpc = Tinkwell.Runlet.Events.Grpc.V1;

namespace Tinkwell.Runlet.Actions;

/// <summary>
/// Runlet that subscribes to the event bus and executes configurable action
/// handlers defined in a <c>.tw</c> configuration file.
/// </summary>
/// <remarks>
/// Configuration settings:
/// <list type="bullet">
///   <item><c>path</c> — Path to the actions <c>.tw</c> file. Defaults to
///     the coordinator's own configuration file.</item>
/// </list>
/// </remarks>
public sealed class ActionsRunlet : IRunlet
{
    public void ConfigureServices(IServiceCollection services, IConfiguration settings)
    {
        var configPath = settings["path"];
        services.AddSingleton(new ActionsRunletOptions(configPath));
        services.AddSingleton<IExpressionEvaluator>(new ExpressionEvaluator());
        services.AddSingleton<IActionHandler, LogActionHandler>();
        services.AddSingleton<IActionHandler, CreateEventActionHandler>();
        services.AddSingleton<IActionHandler, HttpPostActionHandler>();
        services.AddSingleton<IActionHandler, TextSendActionHandler>();
        services.AddHostedService<ActionExecutionWorker>();
    }

    public Task StartAsync(IServiceProvider services, CancellationToken ct) => Task.CompletedTask;
}

internal sealed record ActionsRunletOptions(string? ConfigPath);

internal static class EventBusRequestFactory
{
    public static EventsGrpc.PublishEventRequest ToPublishRequest(EventEnvelope envelope)
    {
        var request = new EventsGrpc.PublishEventRequest
        {
            Source = envelope.Source,
            Verb = (EventsGrpc.EventVerb)(int)envelope.Verb,
            Name = envelope.Name,
            Timestamp = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(
                DateTime.SpecifyKind(envelope.Timestamp, DateTimeKind.Utc)),
        };

        if (envelope.CustomVerb is not null)
            request.CustomVerb = envelope.CustomVerb;
        if (envelope.Object is not null)
            request.Object = envelope.Object;
        if (envelope.CorrelationId is not null)
            request.CorrelationId = envelope.CorrelationId;
        foreach (var (k, v) in envelope.Payload)
            request.Payload[k] = v;

        return request;
    }
}
