using System.Globalization;
using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Tinkwell.Events;
using Tinkwell.Measures;
using Tinkwell.Runlet.Measures;

namespace Tinkwell.Runlet.MeasureEvents;

/// <summary>
/// Forwards every <see cref="IMeasureRegistry.ValueChanged"/> event to the
/// generic event bus. No filtering, no debounce — if advanced behaviour is
/// needed, write a custom runlet.
/// </summary>
internal sealed class MeasureEventsWorker : BackgroundService
{
    private readonly MeasureRegistryHolder _registryHolder;
    private readonly MeasuresConfigReady _measuresReady;
    private readonly EventPublisherHolder _publisherHolder;
    private readonly ILogger<MeasureEventsWorker> _logger;

    private readonly Channel<ValueChangedEventArgs> _channel;
    private readonly ChannelDropTracker _dropTracker;

    public MeasureEventsWorker(
        MeasureRegistryHolder registryHolder,
        MeasuresConfigReady measuresReady,
        MeasureEventsOptions options,
        EventPublisherHolder publisherHolder,
        ILogger<MeasureEventsWorker> logger)
    {
        _registryHolder = registryHolder;
        _measuresReady = measuresReady;
        _publisherHolder = publisherHolder;
        _logger = logger;
        _channel = Channel.CreateBounded<ValueChangedEventArgs>(
            options.ChannelConfig.ToBoundedOptions());
        _dropTracker = new ChannelDropTracker("measure-events", logger);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        IMeasureRegistry registry;
        try
        {
            registry = await _registryHolder.WaitAsync(stoppingToken);
            await _measuresReady.WaitAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        IEventPublisher publisher;
        try
        {
            publisher = await _publisherHolder.WaitAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        _logger.LogDebug("MeasureEventsWorker started — bridging value changes to event bus");

        registry.ValueChanged += OnValueChanged;
        try
        {
            await foreach (var e in _channel.Reader.ReadAllAsync(stoppingToken))
            {
                try
                {
                    var valueStr = e.NewValue.Type == MeasureValueType.Number
                        ? e.NewValue.AsDouble().ToString(CultureInfo.InvariantCulture)
                        : e.NewValue.Type.ToString();

                    await publisher.PublishAsync(new EventEnvelope
                    {
                        Source = "measures",
                        Verb = EventVerb.Changed,
                        Name = e.Name,
                        Object = valueStr,
                        CorrelationId = e.CorrelationId,
                        Timestamp = DateTime.UtcNow,
                    }, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (OutOfMemoryException) { Environment.FailFast("Out of memory"); throw; }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "Failed to publish measure change for '{Name}'", e.Name);
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            registry.ValueChanged -= OnValueChanged;
        }
    }

    private void OnValueChanged(object? sender, ValueChangedEventArgs e)
    {
        _dropTracker.TryWrite(_channel.Writer, e);
    }
}