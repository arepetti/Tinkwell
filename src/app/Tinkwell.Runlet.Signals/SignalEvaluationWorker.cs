using System.Globalization;
using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Tinkwell.Configuration;
using Tinkwell.Runlet.Signals.Configuration;
using Tinkwell.Events;
using Tinkwell.Expressions;
using Tinkwell.Measures;
using Tinkwell.Runner.Hosting;
using Tinkwell.Runlet.Measures;
using UnitsNet;

namespace Tinkwell.Runlet.Signals;

/// <summary>
/// Watches for measure value changes, evaluates signal conditions, and fires
/// signal events. Manages a per-signal state machine with support for
/// sustained-condition durations (<c>for</c>) and hysteresis (<c>until</c>).
/// </summary>
/// <remarks>
/// All events (value changes and duration-elapsed timers) are funnelled
/// through a single <see cref="Channel{T}"/> for sequential processing.
/// </remarks>
internal sealed class SignalEvaluationWorker : BackgroundService
{
    private readonly MeasureRegistryHolder _registryHolder;
    private readonly MeasuresConfigReady _measuresReady;
    private readonly SignalsRunletOptions _options;
    private readonly CoordinatorPipeClient _pipeClient;
    private readonly SignalRegistry _signalRegistry;
    private readonly EventPublisherHolder _publisherHolder;
    private readonly IExpressionEvaluator _evaluator;
    private readonly ILogger<SignalEvaluationWorker> _logger;

    private readonly Channel<SignalChannelEvent> _channel;
    private readonly ChannelDropTracker _dropTracker;

    private IMeasureRegistry? _registry;
    private Dictionary<string, SignalInstance> _signals = new(StringComparer.Ordinal);
    private Dictionary<string, List<string>> _reverseMap = new(StringComparer.Ordinal);

    private readonly TaskCompletionSource _readyTcs = new();

    /// <summary>
    /// Completes when the worker has subscribed to value changes and is
    /// actively processing events.
    /// </summary>
    internal Task ReadyTask => _readyTcs.Task;

    private IEventPublisher? _publisher;

    public SignalEvaluationWorker(
        MeasureRegistryHolder registryHolder,
        MeasuresConfigReady measuresReady,
        SignalsRunletOptions options,
        CoordinatorPipeClient pipeClient,
        SignalRegistry signalRegistry,
        EventPublisherHolder publisherHolder,
        IExpressionEvaluator evaluator,
        ILogger<SignalEvaluationWorker> logger)
    {
        _registryHolder = registryHolder;
        _measuresReady = measuresReady;
        _options = options;
        _pipeClient = pipeClient;
        _signalRegistry = signalRegistry;
        _publisherHolder = publisherHolder;
        _evaluator = evaluator;
        _logger = logger;
        _channel = Channel.CreateBounded<SignalChannelEvent>(
            options.ChannelConfig.ToBoundedOptions());
        _dropTracker = new ChannelDropTracker("signals.events", logger);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            _registry = await _registryHolder.WaitAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _readyTcs.TrySetResult();
            return;
        }

        try
        {
            await _measuresReady.WaitAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _readyTcs.TrySetResult();
            return;
        }

        SignalsConfig config;
        try
        {
            config = await LoadConfigAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _readyTcs.TrySetResult();
            return;
        }
        catch (OutOfMemoryException) { Environment.FailFast("Out of memory"); throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load signals configuration");
            _readyTcs.TrySetResult();
            return;
        }

        if (config.Signals.Count == 0)
        {
            _logger.LogDebug("No signals defined, worker will idle");
            _readyTcs.TrySetResult();
            return;
        }

        try
        {
            _publisher = await _publisherHolder.WaitAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _readyTcs.TrySetResult();
            return;
        }

        BuildSignalInstances(config);
        BuildReverseMap(config);
        _signalRegistry.RegisterRange(config.Signals);

        _logger.LogDebug(
            "Signal evaluation ready: {Count} signal(s), watching {Measures} measure(s)",
            config.Signals.Count, _reverseMap.Count);

        _registry.ValueChanged += OnValueChanged;
        _signalRegistry.SignalAdded += OnSignalAdded;
        _readyTcs.TrySetResult();
        try
        {
            await ProcessEventsAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            _signalRegistry.SignalAdded -= OnSignalAdded;
            _registry.ValueChanged -= OnValueChanged;
            foreach (var inst in _signals.Values)
                inst.CancelPending();
        }
    }

    private void BuildSignalInstances(SignalsConfig config)
    {
        _signals = new Dictionary<string, SignalInstance>(
            config.Signals.Count, StringComparer.Ordinal);

        foreach (var def in config.Signals)
            _signals[def.Name] = new SignalInstance(def);
    }

    private void BuildReverseMap(SignalsConfig config)
    {
        _reverseMap = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        foreach (var def in config.Signals)
            AddToReverseMap(def);
    }

    private void AddToReverseMap(SignalDefinition def)
    {
        var parameters = new HashSet<string>(StringComparer.Ordinal);

        foreach (var p in DependencyWalker<object>.ExtractParameters(def.WhenExpression))
            parameters.Add(p);

        if (def.UntilExpression is not null)
        {
            foreach (var p in DependencyWalker<object>.ExtractParameters(def.UntilExpression))
                parameters.Add(p);
        }

        if (def.Duration is SignalDuration.Expression expr)
        {
            foreach (var p in DependencyWalker<object>.ExtractParameters(expr.Text))
                parameters.Add(p);
        }

        foreach (var measureName in parameters)
        {
            if (!_reverseMap.TryGetValue(measureName, out var list))
            {
                list = [];
                _reverseMap[measureName] = list;
            }
            list.Add(def.Name);
        }
    }

    private void OnValueChanged(object? sender, ValueChangedEventArgs e)
    {
        _dropTracker.TryWrite(_channel.Writer, new MeasureValueChanged(e.Name, e.CorrelationId));
    }

    private void OnSignalAdded(object? sender, SignalAddedEventArgs e)
    {
        _dropTracker.TryWrite(_channel.Writer, new SignalDefinitionAdded(e.Definition));
    }

    private async Task ProcessEventsAsync(CancellationToken ct)
    {
        await foreach (var evt in _channel.Reader.ReadAllAsync(ct))
        {
            switch (evt)
            {
                case MeasureValueChanged mvc:
                    await HandleValueChangedAsync(mvc.MeasureName, mvc.CorrelationId, ct);
                    break;
                case DurationElapsed de:
                    await HandleDurationElapsedAsync(de.SignalName, de.Sequence, ct);
                    break;
                case DurationFailed df:
                    HandleDurationFailed(df.SignalName, df.Sequence);
                    break;
                case SignalDefinitionAdded sda:
                    HandleSignalAdded(sda.Definition);
                    break;
            }
        }
    }

    private void HandleSignalAdded(SignalDefinition def)
    {
        if (_signals.ContainsKey(def.Name))
            return;

        _signals[def.Name] = new SignalInstance(def);
        AddToReverseMap(def);
        _logger.LogDebug("Dynamically added signal '{Name}'", def.Name);
    }

    private async Task HandleValueChangedAsync(
        string measureName, string? correlationId, CancellationToken ct)
    {
        if (!_reverseMap.TryGetValue(measureName, out var signalNames))
            return;

        foreach (var signalName in signalNames)
        {
            if (!_signals.TryGetValue(signalName, out var inst))
                continue;

            inst.CorrelationId = correlationId;
            await EvaluateSignalAsync(inst, ct);
        }
    }

    private async Task EvaluateSignalAsync(SignalInstance inst, CancellationToken ct)
    {
        var def = inst.Definition;

        switch (inst.State)
        {
            case SignalState.Idle:
            {
                var whenTrue = await EvaluateBoolAsync(def.WhenExpression, ct);
                if (!whenTrue)
                    return;

                if (def.Duration is null)
                {
                    FireSignal(inst);
                }
                else
                {
                    inst.State = SignalState.Pending;
                    inst.Sequence++;
                    StartDurationTimer(inst, ct);
                    _logger.LogTrace("Signal '{Name}' entering Pending state", def.Name);
                }
                break;
            }

            case SignalState.Pending:
            {
                var whenTrue = await EvaluateBoolAsync(def.WhenExpression, ct);
                if (!whenTrue)
                {
                    inst.CancelPending();
                    inst.State = SignalState.Idle;
                    _logger.LogTrace("Signal '{Name}' condition cleared, back to Idle", def.Name);
                }
                break;
            }

            case SignalState.Active:
            {
                if (def.UntilExpression is null)
                    return;

                var untilTrue = await EvaluateBoolAsync(def.UntilExpression, ct);
                if (untilTrue)
                {
                    inst.State = SignalState.Idle;
                    _logger.LogTrace("Signal '{Name}' until condition met, back to Idle", def.Name);
                }
                break;
            }
        }
    }

    private async Task HandleDurationElapsedAsync(string signalName, long sequence, CancellationToken ct)
    {
        if (!_signals.TryGetValue(signalName, out var inst))
            return;

        if (inst.State != SignalState.Pending || inst.Sequence != sequence)
            return;

        var whenTrue = await EvaluateBoolAsync(inst.Definition.WhenExpression, ct);
        if (whenTrue)
        {
            FireSignal(inst);
        }
        else
        {
            inst.CancelPending();
            inst.State = SignalState.Idle;
        }
    }

    private void HandleDurationFailed(string signalName, long sequence)
    {
        if (!_signals.TryGetValue(signalName, out var inst))
            return;

        if (inst.State != SignalState.Pending || inst.Sequence != sequence)
            return;

        inst.CancelPending();
        inst.State = SignalState.Idle;
        _logger.LogDebug("Signal '{Name}' duration failed, reset to Idle", signalName);
    }

    private void FireSignal(SignalInstance inst)
    {
        inst.CancelPending();

        var now = DateTime.UtcNow;
        _logger.LogInformation("Signal '{Name}' fired", inst.Definition.Name);

        _signalRegistry.Fire(inst.Definition.Name, now);

        _ = PublishSignalFiredAsync(inst, now);

        if (inst.Definition.UntilExpression is not null)
        {
            inst.State = SignalState.Active;
            _logger.LogTrace("Signal '{Name}' entering Active state (has until clause)", inst.Definition.Name);
        }
        else
        {
            inst.State = SignalState.Idle;
        }
    }

    private async Task PublishSignalFiredAsync(SignalInstance inst, DateTime timestamp)
    {
        if (_publisher is null)
            return;

        try
        {
            await _publisher.PublishAsync(new EventEnvelope
            {
                Source = "signals",
                Verb = EventVerb.Fired,
                Name = inst.Definition.Name,
                CorrelationId = inst.CorrelationId,
                Timestamp = timestamp,
                Payload = inst.Definition.Properties
                    ?? (IReadOnlyDictionary<string, string>)new Dictionary<string, string>(),
            });
        }
        catch (OutOfMemoryException) { Environment.FailFast("Out of memory"); }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to publish signal '{Name}' to event bus",
                inst.Definition.Name);
        }
    }

    private void StartDurationTimer(SignalInstance inst, CancellationToken ct)
    {
        inst.CancelPending();

        var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        inst.PendingCts = cts;
        var seq = inst.Sequence;
        var name = inst.Definition.Name;

        _ = Task.Run(async () =>
        {
            try
            {
                var delay = await ResolveDurationAsync(inst.Definition.Duration!, cts.Token);
                await Task.Delay(delay, cts.Token);
                _dropTracker.TryWrite(_channel.Writer, new DurationElapsed(name, seq));
            }
            catch (OperationCanceledException)
            {
            }
            catch (OutOfMemoryException) { Environment.FailFast("Out of memory"); }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Duration timer failed for signal '{Name}', resetting to Idle", name);
                _dropTracker.TryWrite(_channel.Writer, new DurationFailed(name, seq));
            }
            finally
            {
                cts.Dispose();
            }
        }, CancellationToken.None);
    }

    private async Task<TimeSpan> ResolveDurationAsync(SignalDuration duration, CancellationToken ct)
    {
        return duration switch
        {
            SignalDuration.Seconds s => TimeSpan.FromSeconds(s.Value),
            SignalDuration.Parsed p => ParseDurationString(p.Text),
            SignalDuration.Expression e => TimeSpan.FromSeconds(
                await EvaluateDoubleAsync(e.Text, ct)),
            _ => throw new InvalidOperationException($"Unknown duration type: {duration.GetType().Name}")
        };
    }

    private static TimeSpan ParseDurationString(string text)
    {
        var parsed = Duration.Parse(text);
        return parsed.ToTimeSpan();
    }

    private async Task<bool> EvaluateBoolAsync(string expression, CancellationToken ct)
    {
        try
        {
            var parameters = await BuildParametersAsync(expression, ct);
            return await _evaluator.EvaluateBooleanAsync(expression, parameters, cancellationToken: ct);
        }
        catch (OutOfMemoryException) { Environment.FailFast("Out of memory"); throw; }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to evaluate expression: {Expression}", expression);
            return false;
        }
    }

    private async Task<double> EvaluateDoubleAsync(string expression, CancellationToken ct)
    {
        var parameters = await BuildParametersAsync(expression, ct);
        var result = await _evaluator.EvaluateAsync(expression, parameters, cancellationToken: ct);
        return Convert.ToDouble(result, CultureInfo.InvariantCulture);
    }

    private async Task<Dictionary<string, object?>> BuildParametersAsync(
        string expression, CancellationToken ct)
    {
        var paramNames = DependencyWalker<object>.ExtractParameters(expression);
        var parameters = new Dictionary<string, object?>(paramNames.Count, StringComparer.Ordinal);

        foreach (var name in paramNames)
        {
            var measure = await _registry!.FindAsync(name, ct);
            if (measure is not null && measure.Value.Type == MeasureValueType.Number)
                parameters[name] = measure.Value.AsDouble();
            else
                parameters[name] = null;
        }

        return parameters;
    }

    private async Task<SignalsConfig> LoadConfigAsync(CancellationToken ct)
    {
        string configPath;
        if (!string.IsNullOrWhiteSpace(_options.ConfigPath))
        {
            configPath = _options.ConfigPath;
        }
        else
        {
            _logger.LogDebug("No path configured, fetching coordinator config path");
            configPath = await _pipeClient.FetchConfigPathAsync(ct);
        }

        _logger.LogDebug("Loading signals from: {Path}", configPath);
        var parser = new SignalsParser(logger: _logger);
        var config = await parser.LoadFileAsync(configPath);
        _logger.LogInformation(
            "Signals config loaded: {Count} signal(s) from {Path}",
            config.Signals.Count, configPath);
        return config;
    }
}

internal abstract record SignalChannelEvent;
internal sealed record MeasureValueChanged(string MeasureName, string? CorrelationId) : SignalChannelEvent;
internal sealed record DurationElapsed(string SignalName, long Sequence) : SignalChannelEvent;
internal sealed record DurationFailed(string SignalName, long Sequence) : SignalChannelEvent;
internal sealed record SignalDefinitionAdded(SignalDefinition Definition) : SignalChannelEvent;

/// <summary>
/// Event arguments raised when a signal fires.
/// </summary>
public sealed class SignalFiredEventArgs : EventArgs
{
    public string SignalName { get; }
    public DateTime Timestamp { get; }
    public IReadOnlyDictionary<string, string> Properties { get; }

    public SignalFiredEventArgs(string signalName, DateTime timestamp,
        IReadOnlyDictionary<string, string>? properties = null)
    {
        SignalName = signalName;
        Timestamp = timestamp;
        Properties = properties ?? new Dictionary<string, string>();
    }
}