using System.Collections.Concurrent;
using Microsoft.Extensions.Logging.Abstractions;
using Tinkwell.Configuration;
using Tinkwell.Configuration.Parser;
using Tinkwell.Runlet.Signals.Configuration;
using Tinkwell.Expressions;
using Tinkwell.Expressions.Functions;
using Tinkwell.Measures;
using Tinkwell.Measures.Functions;
using Tinkwell.Runner.Hosting;
using Tinkwell.Runlet.Measures;
using Tinkwell.Runlet.Measures.Configuration;
using Tinkwell.Events;
using Tinkwell.Runlet.Signals;

namespace Tinkwell.Runlet.Signals.Tests;

public class SignalEvaluationWorkerTests
{
    private static readonly IExpressionEvaluator Evaluator = new ExpressionEvaluator(
        ExpressionFunctionDiscovery.BuiltIn()
            .Concat(ExpressionFunctionDiscovery.FromAssemblyOf<QuantityFunction>())
            .ToList());

    private static readonly SourceLocation Loc = new("test", 1, 1);

    /// <summary>
    /// Minimal IMeasureRegistry that stores values in memory and raises
    /// ValueChanged when updated. WatchAsync blocks until cancelled.
    /// </summary>
    private sealed class FakeRegistry : IMeasureRegistry
    {
        private readonly ConcurrentDictionary<string, Measure> _measures = new(StringComparer.Ordinal);

        public event EventHandler<ValueChangedEventArgs>? ValueChanged;

        public Task RegisterAsync(MeasureDefinition definition, MeasureMetadata? metadata = null,
            MeasureValue? initialValue = null, CancellationToken ct = default)
        {
            var value = initialValue ?? MeasureValue.Undefined;
            _measures[definition.Name] = new Measure
            {
                Definition = definition,
                Metadata = metadata ?? new MeasureMetadata(),
                Value = value,
            };
            return Task.CompletedTask;
        }

        public Task<Measure?> FindAsync(string name, CancellationToken ct = default)
        {
            _measures.TryGetValue(name, out var measure);
            return Task.FromResult(measure);
        }

        public Task UpdateAsync(string name, MeasureValue value,
            string? correlationId = null, CancellationToken ct = default)
        {
            if (_measures.TryGetValue(name, out var existing))
            {
                var old = existing.Value;
                _measures[name] = existing with { Value = value };
                ValueChanged?.Invoke(this, new ValueChangedEventArgs
                {
                    Name = name,
                    OldValue = old,
                    NewValue = value,
                    CorrelationId = correlationId,
                });
            }
            return Task.CompletedTask;
        }

        public async Task UpdateManyAsync(IEnumerable<(string Name, MeasureValue Value)> updates,
            string? correlationId = null, CancellationToken ct = default)
        {
            foreach (var (name, value) in updates)
            {
                await UpdateAsync(name, value, correlationId, ct);
            }
        }

        public Task<IReadOnlyList<Measure>> FindAllAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Measure>>(_measures.Values.ToList());

        public Task<MeasureDefinition?> FindDefinitionAsync(string name, CancellationToken ct = default)
        {
            _measures.TryGetValue(name, out var m);
            return Task.FromResult(m?.Definition);
        }

        public async Task WatchAsync(CancellationToken ct = default)
        {
            try { await Task.Delay(Timeout.Infinite, ct); }
            catch (OperationCanceledException)
            {
            }
        }
    }

    private sealed class NoOpEventPublisher : IEventPublisher
    {
        public Task PublishAsync(EventEnvelope envelope, CancellationToken ct = default)
            => Task.CompletedTask;
    }

    private static MeasureDefinition ScalarDef(string name) => new()
    {
        Name = name,
        Type = MeasureType.Number,
        QuantityType = "Scalar",
        Unit = "Amount",
    };

    private static MeasureValue NumericValue(double v) =>
        MeasureValue.FromValue(ScalarDef("_"), v, DateTime.UtcNow);

    private static SignalDefinition Signal(
        string name, string when, string? until = null, SignalDuration? duration = null)
        => new(name, when, until, duration, null, new Dictionary<string, string>(), Loc);

    private async Task<(SignalEvaluationWorker Worker, FakeRegistry Registry, List<string> FiredSignals, SignalRegistry SignalRegistry)>
        SetupAsync(
            IReadOnlyList<SignalDefinition> signals,
            Dictionary<string, double> initialValues,
            string? configFilePath = null)
    {
        var registry = new FakeRegistry();
        foreach (var (name, value) in initialValues)
        {
            var def = ScalarDef(name);
            await registry.RegisterAsync(def, initialValue: NumericValue(value));
        }

        var holder = new MeasureRegistryHolder();
        holder.Set(registry);

        var measuresReady = new MeasuresConfigReady();
        measuresReady.Set(new MeasuresConfig([]));

        var configPath = configFilePath is not null ? configFilePath : WriteSignalConfig(signals);

        var signalRegistry = new SignalRegistry();
        var publisherHolder = new EventPublisherHolder();
        publisherHolder.Set(new NoOpEventPublisher());
        var options = new SignalsRunletOptions(configPath,
            new ChannelConfig(512, System.Threading.Channels.BoundedChannelFullMode.DropOldest), true);
        var pipeClient = new CoordinatorPipeClient(
            "unused", NullLogger.Instance);
        var worker = new SignalEvaluationWorker(
            holder, measuresReady, options, pipeClient, signalRegistry, publisherHolder, Evaluator,
            NullLogger<SignalEvaluationWorker>.Instance);

        var firedSignals = new List<string>();
        signalRegistry.SignalFired += (_, e) => firedSignals.Add(e.SignalName);

        return (worker, registry, firedSignals, signalRegistry);
    }

    private static string WriteSignalConfig(IReadOnlyList<SignalDefinition> signals)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var s in signals)
        {
            sb.Append($"signal {s.Name} when \"{s.WhenExpression}\"");
            if (s.UntilExpression is not null)
                sb.Append($" until \"{s.UntilExpression}\"");
            if (s.Duration is SignalDuration.Seconds sec)
                sb.Append($" for {sec.Value}");
            sb.AppendLine(" { }");
        }

        var path = Path.Combine(Path.GetTempPath(), $"tw-signal-test-{Guid.NewGuid():N}.tw");
        File.WriteAllText(path, sb.ToString());
        return path;
    }

    private static async Task WaitForReadyAsync(SignalEvaluationWorker worker, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        await worker.ReadyTask.WaitAsync(timeout.Token);
    }

    private static async Task WaitForChannelDrainAsync(int ms = 200)
    {
        await Task.Delay(ms);
    }

    [Fact]
    public async Task WhenTrue_FiresImmediately()
    {
        var signals = new[] { Signal("alert", "temp > 50") };
        var (worker, registry, fired, _) = await SetupAsync(signals,
            new() { ["temp"] = 40.0 });

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var workerTask = worker.StartAsync(cts.Token);
        await WaitForReadyAsync(worker, cts.Token);

        await registry.UpdateAsync("temp", NumericValue(60.0));
        await WaitForChannelDrainAsync();

        cts.Cancel();
        try
        {
            await workerTask;
        }
        catch (OperationCanceledException)
        {
        }

        Assert.Contains("alert", fired);
    }

    [Fact]
    public async Task WhenFalse_DoesNotFire()
    {
        var signals = new[] { Signal("alert", "temp > 50") };
        var (worker, registry, fired, _) = await SetupAsync(signals,
            new() { ["temp"] = 40.0 });

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var workerTask = worker.StartAsync(cts.Token);
        await WaitForReadyAsync(worker, cts.Token);

        await registry.UpdateAsync("temp", NumericValue(45.0));
        await WaitForChannelDrainAsync();

        cts.Cancel();
        try
        {
            await workerTask;
        }
        catch (OperationCanceledException)
        {
        }

        Assert.Empty(fired);
    }

    [Fact]
    public async Task WithDuration_FiresAfterDelay()
    {
        var signals = new[]
        {
            Signal("alert", "temp > 50", duration: new SignalDuration.Seconds(1))
        };
        var (worker, registry, fired, _) = await SetupAsync(signals,
            new() { ["temp"] = 40.0 });

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var workerTask = worker.StartAsync(cts.Token);
        await WaitForReadyAsync(worker, cts.Token);

        await registry.UpdateAsync("temp", NumericValue(60.0));
        await WaitForChannelDrainAsync(400);
        Assert.Empty(fired);

        await Task.Delay(2000);
        Assert.Contains("alert", fired);

        cts.Cancel();
        try
        {
            await workerTask;
        }
        catch (OperationCanceledException)
        {
        }
    }

    [Fact]
    public async Task WithDuration_CancelledIfConditionClears()
    {
        var signals = new[]
        {
            Signal("alert", "temp > 50", duration: new SignalDuration.Seconds(2))
        };
        var (worker, registry, fired, _) = await SetupAsync(signals,
            new() { ["temp"] = 40.0 });

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var workerTask = worker.StartAsync(cts.Token);
        await WaitForReadyAsync(worker, cts.Token);

        await registry.UpdateAsync("temp", NumericValue(60.0));
        await WaitForChannelDrainAsync();

        await registry.UpdateAsync("temp", NumericValue(40.0));
        await Task.Delay(3000);

        Assert.Empty(fired);

        cts.Cancel();
        try
        {
            await workerTask;
        }
        catch (OperationCanceledException)
        {
        }
    }

    [Fact]
    public async Task Hysteresis_SuppressesRefire()
    {
        var signals = new[]
        {
            Signal("alert", "temp > 80", until: "temp < 70")
        };
        var (worker, registry, fired, _) = await SetupAsync(signals,
            new() { ["temp"] = 60.0 });

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var workerTask = worker.StartAsync(cts.Token);
        await WaitForReadyAsync(worker, cts.Token);

        await registry.UpdateAsync("temp", NumericValue(85.0));
        await WaitForChannelDrainAsync();
        Assert.Single(fired);

        await registry.UpdateAsync("temp", NumericValue(90.0));
        await WaitForChannelDrainAsync();
        Assert.Single(fired);

        await registry.UpdateAsync("temp", NumericValue(65.0));
        await WaitForChannelDrainAsync();

        await registry.UpdateAsync("temp", NumericValue(85.0));
        await WaitForChannelDrainAsync();
        Assert.Equal(2, fired.Count);

        cts.Cancel();
        try
        {
            await workerTask;
        }
        catch (OperationCanceledException)
        {
        }
    }

    /// <summary>
    /// <see cref="SignalRegistry.Register"/> (used by <c>SignalsGrpcService.Create</c>) raises
    /// <see cref="SignalRegistry.SignalAdded"/>; the worker must add the instance and re-map
    /// measure dependencies so new conditions are evaluated. Covers the runtime (gRPC) add path
    /// without running a gRPC host.
    /// </summary>
    [Fact]
    public async Task RegisterAtRuntime_MatchesGrpcCreateSemantics_FiresOnMeasureUpdate()
    {
        var signals = new[] { Signal("keepalive", "1 == 0") };
        var (worker, registry, fired, signalRegistry) = await SetupAsync(signals,
            new() { ["temp"] = 40.0 });

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var workerTask = worker.StartAsync(cts.Token);
        await WaitForReadyAsync(worker, cts.Token);

        signalRegistry.Register(
            new SignalDefinition(
                "runtime_alert",
                "temp > 50",
                null,
                null,
                null,
                new Dictionary<string, string>(),
                Loc));

        await registry.UpdateAsync("temp", NumericValue(60.0));
        await WaitForChannelDrainAsync();

        cts.Cancel();
        try
        {
            await workerTask;
        }
        catch (OperationCanceledException)
        {
        }

        Assert.Contains("runtime_alert", fired);
    }

    /// <summary>
    /// Two signal definitions that depend on the same measure are both in the reverse map;
    /// one measure change must evaluate and potentially fire each. Confirms the map uses lists,
    /// not single slots per measure.
    /// </summary>
    [Fact]
    public async Task TwoSignalsOnSameMeasure_BothCanFire()
    {
        var signals = new[]
        {
            Signal("a", "temp > 50"),
            Signal("b", "temp > 50"),
        };
        var (worker, registry, fired, _) = await SetupAsync(signals, new() { ["temp"] = 40.0 });

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var workerTask = worker.StartAsync(cts.Token);
        await WaitForReadyAsync(worker, cts.Token);

        await registry.UpdateAsync("temp", NumericValue(60.0));
        await WaitForChannelDrainAsync();

        cts.Cancel();
        try
        {
            await workerTask;
        }
        catch (OperationCanceledException)
        {
        }

        Assert.Contains("a", fired);
        Assert.Contains("b", fired);
    }

    /// <summary>
    /// <see cref="SignalEvaluationWorker.ExecuteAsync"/> must complete <see cref="SignalEvaluationWorker.ReadyTask"/>
    /// on config load errors (e.g. missing file) so host startup does not hang. Regression for the
    /// fix that sets <c>_readyTcs</c> in the <c>catch (Exception)</c> path around
    /// <c>LoadConfigAsync</c>.
    /// </summary>
    [Fact]
    public async Task ConfigLoadFailure_ReadyTaskCompletes()
    {
        var missingPath = Path.Combine(Path.GetTempPath(), $"tw-signals-missing-{Guid.NewGuid():N}.tw");
        if (File.Exists(missingPath))
        {
            File.Delete(missingPath);
        }

        var (worker, _, _, _) = await SetupAsync(
            [],
            new Dictionary<string, double>(),
            configFilePath: missingPath);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var workerTask = worker.StartAsync(cts.Token);
        await worker.ReadyTask.WaitAsync(cts.Token);

        cts.Cancel();
        try
        {
            await workerTask;
        }
        catch (OperationCanceledException)
        {
        }
    }
}
