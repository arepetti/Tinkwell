using System.Globalization;
using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Tinkwell.Configuration;
using Tinkwell.Runlet.Measures.Configuration;
using Tinkwell.Configuration.Parser;
using Tinkwell.Expressions;
using Tinkwell.Health;
using Tinkwell.Measures;
using Tinkwell.Runner.Hosting;

namespace Tinkwell.Runlet.Measures;

/// <summary>
/// Loads measure definitions from config, registers them with the
/// <see cref="IMeasureRegistry"/>, then watches for value changes and
/// recalculates derived measures whose expressions depend on the changed
/// value. Uses <see cref="DependencyWalker{TItem}"/> to determine a safe
/// evaluation order (topological sort) and cascades updates through the
/// dependency graph.
/// </summary>
/// <remarks>
/// Events are funnelled through a <see cref="Channel{T}"/> so that
/// recalculations are processed sequentially, avoiding concurrent access
/// to shared state. The <see cref="DependencyWalker{TItem}"/> tracks all
/// expression parameters (including source measures not in the analysis
/// set) in its reverse dependency map, so source-measure changes
/// correctly trigger derived-measure recalculation.
/// </remarks>
internal sealed class DerivedMeasureWorker : BackgroundService
{
    private readonly MeasureRegistryHolder _registryHolder;
    private readonly MeasuresConfigReady _configReady;
    private readonly MeasuresRunletOptions _options;
    private readonly CoordinatorPipeClient _pipeClient;
    private readonly IExpressionEvaluator _evaluator;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly ILogger<DerivedMeasureWorker> _logger;

    private readonly Channel<ValueChangedEventArgs> _eventChannel;
    private readonly ChannelDropTracker _dropTracker;

    private DependencyAnalysis<MeasureConfigEntry>? _analysis;
    private IMeasureRegistry? _registry;
    private readonly HashSet<string> _disabled = new(StringComparer.Ordinal);

    public DerivedMeasureWorker(
        MeasureRegistryHolder registryHolder,
        MeasuresConfigReady configReady,
        MeasuresRunletOptions options,
        CoordinatorPipeClient pipeClient,
        ChannelBackpressureCheck backpressure,
        IExpressionEvaluator evaluator,
        IHostApplicationLifetime lifetime,
        ILogger<DerivedMeasureWorker> logger)
    {
        _registryHolder = registryHolder;
        _configReady = configReady;
        _options = options;
        _pipeClient = pipeClient;
        _evaluator = evaluator;
        _lifetime = lifetime;
        _logger = logger;
        _eventChannel = Channel.CreateBounded<ValueChangedEventArgs>(
            options.DerivedChannelConfig.ToBoundedOptions());
        _dropTracker = new ChannelDropTracker("measures.derived", logger);
        backpressure.Attach(() => _eventChannel.Reader.Count);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            _registry = await _registryHolder.WaitAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        MeasuresConfig config;
        var configReadySet = false;
        try
        {
            config = await LoadAndRegisterMeasuresAsync(stoppingToken);
            configReadySet = true;
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (OutOfMemoryException) { Environment.FailFast("Out of memory"); throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load measures configuration");
            return;
        }
        finally
        {
            if (!configReadySet)
            {
                _configReady.Set(new MeasuresConfig(Array.Empty<MeasureConfigEntry>()));
            }
        }

        var derivedEntries = config.Measures
            .Where(e => e.Definition.Attributes.HasFlag(MeasureAttributes.Derived))
            .ToList();

        if (derivedEntries.Count == 0)
        {
            _logger.LogDebug("No derived measures found, worker will idle");
            return;
        }

        var walker = new DependencyWalker<MeasureConfigEntry>(
            entry => entry.Definition.Name,
            entry => entry.Value);

        try
        {
            _analysis = walker.Analyze(derivedEntries);
        }
        catch (CircularDependencyException ex)
        {
            _logger.LogCritical(ex,
                "Circular dependency in derived measures, aborting. Involved: {Measures}",
                string.Join(", ", ex.CycleParticipants));
            return;
        }

        _logger.LogDebug(
            "Dependency analysis complete: {Count} derived measure(s) in calculation order",
            _analysis.CalculationOrder.Count);

        await CalculateInitialValuesAsync(stoppingToken);

        if (_analysis.ReverseDependencies.Count == 0)
        {
            _logger.LogDebug("No dependencies to watch, worker will idle");
            return;
        }

        _registry.ValueChanged += OnValueChanged;
        try
        {
            await ProcessEventsAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            _registry.ValueChanged -= OnValueChanged;
        }
    }

    private async Task CalculateInitialValuesAsync(CancellationToken ct)
    {
        foreach (var entry in _analysis!.CalculationOrder)
        {
            if (ct.IsCancellationRequested)
                break;

            await RecalculateAsync(entry, ct);
        }
    }

    private void OnValueChanged(object? sender, ValueChangedEventArgs e)
    {
        _dropTracker.TryWrite(_eventChannel.Writer, e);
    }

    private async Task ProcessEventsAsync(CancellationToken ct)
    {
        await foreach (var e in _eventChannel.Reader.ReadAllAsync(ct))
        {
            if (_analysis is null || _registry is null)
                continue;

            if (_disabled.Contains(e.Name))
                continue;

            if (!_analysis.ReverseDependencies.TryGetValue(e.Name, out var affectedNames))
                continue;

            var affectedSet = new HashSet<string>(affectedNames, StringComparer.Ordinal);
            var updates = new List<(string Name, MeasureValue Value)>();
            var pendingValues = new Dictionary<string, double>(StringComparer.Ordinal);

            foreach (var entry in _analysis.CalculationOrder)
            {
                var name = entry.Definition.Name;
                if (!affectedSet.Contains(name) || _disabled.Contains(name))
                    continue;

                var value = await EvaluateAsync(entry, pendingValues, ct);
                if (value is null)
                    continue;

                updates.Add((name, value.Value));
                pendingValues[name] = value.Value.AsDouble();

                if (_analysis.ReverseDependencies.TryGetValue(name, out var cascaded))
                {
                    foreach (var c in cascaded)
                        affectedSet.Add(c);
                }
            }

            if (updates.Count > 0)
            {
                try
                {
                    await _registry.UpdateManyAsync(updates, e.CorrelationId, ct);
                }
                catch (OutOfMemoryException) { Environment.FailFast("Out of memory"); throw; }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "Failed to push {Count} derived value update(s)", updates.Count);
                }
            }
        }
    }

    private async Task RecalculateAsync(MeasureConfigEntry entry, CancellationToken ct)
    {
        var value = await EvaluateAsync(entry, pendingValues: null, ct);
        if (value is null)
            return;

        try
        {
            await _registry!.UpdateAsync(entry.Definition.Name, value.Value, ct: ct);
            _logger.LogTrace("Recalculated {Name}", entry.Definition.Name);
        }
        catch (OutOfMemoryException) { Environment.FailFast("Out of memory"); throw; }
        catch (Exception ex)
        {
            DispatchMeasureError(entry.OnError, ex, entry.Definition.Name,
                "Failed to update derived measure");
        }
    }

    private async Task<MeasureValue?> EvaluateAsync(
        MeasureConfigEntry entry,
        IReadOnlyDictionary<string, double>? pendingValues,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(entry.Value))
            return null;

        var name = entry.Definition.Name;

        if (_disabled.Contains(name))
            return null;

        var deps = _analysis!.ForwardDependencies[name];
        var parameters = new Dictionary<string, object?>(deps.Count, StringComparer.Ordinal);

        foreach (var dep in deps)
        {
            if (pendingValues is not null && pendingValues.TryGetValue(dep, out var pending))
            {
                parameters[dep] = pending;
                continue;
            }

            var measure = await _registry!.FindAsync(dep, ct);
            if (measure is not null && measure.Value.Type == MeasureValueType.Number)
            {
                parameters[dep] = measure.Value.AsDouble();
            }
            else
            {
                _logger.LogTrace(
                    "Skipping '{Name}': dependency '{Dep}' not yet available", name, dep);
                return null;
            }
        }

        var policy = entry.OnError;
        var maxAttempts = 1 + (policy?.Retry?.Count ?? 0);
        Exception? lastEx = null;

        for (int attempt=0; attempt < maxAttempts; ++attempt)
        {
            try
            {
                var result = await _evaluator.EvaluateAsync(
                    entry.Value, parameters, cancellationToken: ct);

                if (result is null)
                {
                    DispatchMeasureError(policy, null, name,
                        "Expression evaluated to null");
                    return null;
                }

                var numericResult = Convert.ToDouble(result, CultureInfo.InvariantCulture);
                return MeasureValue.FromValue(entry.Definition, numericResult, DateTime.UtcNow);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (OutOfMemoryException) { Environment.FailFast("Out of memory"); throw; }
            catch (Exception ex)
            {
                lastEx = ex;
                if (attempt < maxAttempts - 1)
                {
                    var retry = policy!.Retry!;
                    var delay = (int)(retry.DelayMs * Math.Pow(retry.BackoffMultiplier, attempt));
                    _logger.LogWarning(ex,
                        "Expression evaluation for '{Name}' failed (attempt {Attempt}/{Max}), retrying in {Delay}ms",
                        name, attempt + 1, retry.Count, delay);
                    await Task.Delay(delay, ct);
                }
            }
        }

        DispatchMeasureError(policy, lastEx, name, "Expression evaluation failed");
        return null;
    }

    private void DispatchMeasureError(
        ErrorPolicy? policy, Exception? ex, string name, string message)
    {
        var action = policy?.Action ?? ErrorPolicyAction.ResumeNext;

        switch (action)
        {
            case ErrorPolicyAction.ResumeNext:
                if (ex is not null)
                    _logger.LogWarning(ex, "{Message} '{Name}', resuming", message, name);
                else
                    _logger.LogWarning("{Message} '{Name}', resuming", message, name);
                break;

            case ErrorPolicyAction.StopThis:
                if (ex is not null)
                    _logger.LogError(ex, "{Message} '{Name}', disabling", message, name);
                else
                    _logger.LogError("{Message} '{Name}', disabling", message, name);
                _disabled.Add(name);
                break;

            case ErrorPolicyAction.StopApplication:
                if (ex is not null)
                    _logger.LogCritical(ex, "{Message} '{Name}', stopping application", message, name);
                else
                    _logger.LogCritical("{Message} '{Name}', stopping application", message, name);
                _lifetime.StopApplication();
                break;

            case ErrorPolicyAction.Publish:
                if (ex is not null)
                    _logger.LogWarning(ex, "{Message} '{Name}', publish not available in measures, resuming", message, name);
                else
                    _logger.LogWarning("{Message} '{Name}', publish not available in measures, resuming", message, name);
                break;
        }
    }

    private async Task<MeasuresConfig> LoadAndRegisterMeasuresAsync(CancellationToken ct)
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

        _logger.LogDebug("Loading measures from: {Path}", configPath);
        var parser = new MeasuresParser(
            logger: _logger,
            options: new ParserOptions { Lax = true });
        var config = await parser.LoadFileAsync(configPath);

        _logger.LogDebug("Registering {Count} measure(s)", config.Measures.Count);

        foreach (var entry in config.Measures)
        {
            if (ct.IsCancellationRequested)
                break;

            try
            {
                MeasureValue? initialValue = null;
                if (entry.Value is not null
                    && !entry.Definition.Attributes.HasFlag(MeasureAttributes.Derived)
                    && double.TryParse(entry.Value, CultureInfo.InvariantCulture, out var numericValue))
                {
                    initialValue = MeasureValue.FromValue(
                        entry.Definition, numericValue, DateTime.UtcNow);
                }

                await _registry!.RegisterAsync(
                    entry.Definition, entry.Metadata, initialValue, ct);

                _logger.LogDebug(
                    "Registered measure '{Name}' (type: {Type}, attributes: {Attrs}{InitialValue})",
                    entry.Definition.Name, entry.Definition.Type, entry.Definition.Attributes,
                    initialValue is not null ? $", initial: {initialValue}" : "");
            }
            catch (OutOfMemoryException) { Environment.FailFast("Out of memory"); throw; }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex,
                    "Failed to register measure '{Name}'", entry.Definition.Name);
            }
        }

        _configReady.Set(config);

        _logger.LogInformation(
            "Measures config loaded: {Count} measure(s) from {Path}",
            config.Measures.Count, configPath);

        return config;
    }
}