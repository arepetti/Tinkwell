using System.Threading.Channels;
using Grpc.Core;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Tinkwell.Measures.History;
using Tinkwell.Runner;
using MeasuresGrpc = Tinkwell.Runlet.Measures.Grpc.V1.Measures;
using V1 = Tinkwell.Runlet.Measures.Grpc.V1;

namespace Tinkwell.Runlet.MeasureHistory;

/// <summary>
/// Subscribes to the remote measures <c>Watch</c> stream and writes points to
/// <see cref="IMeasureHistoryStore"/> using timed and size-based batching.
/// </summary>
internal sealed class MeasureHistoryWorker : BackgroundService
{
    private readonly MeasureHistoryStoreHolder _holder;
    private readonly MeasureHistoryOptions _options;
    private readonly IServiceDiscovery _discovery;
    private readonly ILogger<MeasureHistoryWorker> _logger;

    private static readonly TimeSpan InitialReconnectDelay = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan MaxReconnectDelay = TimeSpan.FromSeconds(60);

    public MeasureHistoryWorker(
        MeasureHistoryStoreHolder holder,
        MeasureHistoryOptions options,
        IServiceDiscovery discovery,
        ILogger<MeasureHistoryWorker> logger)
    {
        _holder = holder;
        _options = options;
        _discovery = discovery;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var store = await _holder.WaitAsync(stoppingToken);

        var delay = InitialReconnectDelay;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunMeasuresSessionAsync(store, stoppingToken);
                delay = InitialReconnectDelay;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.Unavailable)
            {
                _logger.LogWarning(
                    "Measures service unavailable, retrying in {Delay}s",
                    delay.TotalSeconds);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (OutOfMemoryException) { Environment.FailFast("Out of memory"); }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Measure history session failed, retrying in {Delay}s",
                    delay.TotalSeconds);
            }

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            delay = TimeSpan.FromSeconds(
                Math.Min(delay.TotalSeconds * 2, MaxReconnectDelay.TotalSeconds));
        }
    }

    private async Task RunMeasuresSessionAsync(IMeasureHistoryStore store, CancellationToken ct)
    {
        var client = await _discovery.CreateInstanceAsync<MeasuresGrpc.MeasuresClient>("measures", ct);
        await SyncDefinitionsAsync(client, store, ct);

        var channel = Channel.CreateBounded<MeasureHistoryPoint>(
            new BoundedChannelOptions(_options.BatchSize * 4)
            {
                FullMode = BoundedChannelFullMode.DropWrite,
                SingleReader = true,
                SingleWriter = true,
            });

        var dropTracker = new ChannelDropTracker("measure-history.buffer", _logger);
        var consumer = ProcessChannelAsync(channel.Reader, store, ct);

        try
        {
            using var call = client.Watch(new V1.WatchMeasuresRequest(), cancellationToken: ct);
            await foreach (var ev in call.ResponseStream.ReadAllAsync(ct))
            {
                var point = ToHistoryPoint(ev);
                dropTracker.TryWrite(channel.Writer, point);
            }
        }
        finally
        {
            channel.Writer.Complete();
            try
            {
                await consumer;
            }
            catch (OperationCanceledException)
            {
            }
        }
    }

    private async Task SyncDefinitionsAsync(MeasuresGrpc.MeasuresClient client, IMeasureHistoryStore store, CancellationToken ct)
    {
        var list = await client.ListAsync(new V1.ListMeasuresRequest(), cancellationToken: ct);
        foreach (var m in list.Measures)
        {
            try
            {
                await store.SyncDefinitionAsync(ToSnapshot(m), ct);
            }
            catch (OperationCanceledException) { throw; }
            catch (OutOfMemoryException) { Environment.FailFast("Out of memory"); }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to sync definition for measure '{Name}'", m.Definition?.Name);
            }
        }
    }

    private async Task ProcessChannelAsync(
        ChannelReader<MeasureHistoryPoint> reader,
        IMeasureHistoryStore store,
        CancellationToken ct)
    {
        var pending = new List<MeasureHistoryPoint>(_options.BatchSize * 2);
        var gate = new object();
        using var timerCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        async Task FlushChunksAsync(List<MeasureHistoryPoint> batch)
        {
            for (var i = 0; i < batch.Count; i += _options.BatchSize)
            {
                var n = Math.Min(_options.BatchSize, batch.Count - i);
                var chunk = batch.GetRange(i, n);
                await store.WriteManyAsync(chunk, ct).ConfigureAwait(false);
            }
        }

        async Task ConsumerAsync()
        {
            await foreach (var item in reader.ReadAllAsync(ct).ConfigureAwait(false))
            {
                List<MeasureHistoryPoint>? sizeFlush = null;
                lock (gate)
                {
                    pending.Add(item);
                    if (pending.Count >= _options.BatchSize)
                    {
                        sizeFlush = pending.GetRange(0, _options.BatchSize);
                        pending.RemoveRange(0, _options.BatchSize);
                    }
                }

                if (sizeFlush is not null)
                    await store.WriteManyAsync(sizeFlush, ct).ConfigureAwait(false);
            }
        }

        async Task TimerAsync()
        {
            using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(_options.FlushIntervalMs));
            try
            {
                while (await timer.WaitForNextTickAsync(timerCts.Token).ConfigureAwait(false))
                {
                    List<MeasureHistoryPoint>? snapshot = null;
                    lock (gate)
                    {
                        if (pending.Count > 0)
                        {
                            snapshot = new List<MeasureHistoryPoint>(pending);
                            pending.Clear();
                        }
                    }

                    if (snapshot is not null)
                        await FlushChunksAsync(snapshot).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        var timerTask = TimerAsync();
        try
        {
            await ConsumerAsync().ConfigureAwait(false);
        }
        finally
        {
            await timerCts.CancelAsync().ConfigureAwait(false);
            try
            {
                await timerTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }

            List<MeasureHistoryPoint>? remainder;
            lock (gate)
            {
                remainder = pending.Count > 0 ? new List<MeasureHistoryPoint>(pending) : null;
                pending.Clear();
            }

            if (remainder is not null)
                await FlushChunksAsync(remainder).ConfigureAwait(false);
        }
    }

    internal static MeasureDefinitionSnapshot ToSnapshot(V1.MeasureProto m)
    {
        var d = m.Definition;
        var meta = m.Metadata;

        return new MeasureDefinitionSnapshot
        {
            Name = d.Name,
            Type = d.Type,
            QuantityType = string.IsNullOrEmpty(d.QuantityType) ? null : d.QuantityType,
            Unit = string.IsNullOrEmpty(d.Unit) ? null : d.Unit,
            Minimum = d.HasMinimum ? d.Minimum : null,
            Maximum = d.HasMaximum ? d.Maximum : null,
            Precision = d.HasPrecision ? d.Precision : null,
            Description = string.IsNullOrEmpty(meta.Description) ? null : meta.Description,
            Category = string.IsNullOrEmpty(meta.Category) ? null : meta.Category,
            Tags = meta.Tags.Count > 0 ? meta.Tags.ToList() : [],
        };
    }

    internal static MeasureHistoryPoint ToHistoryPoint(V1.MeasureEvent ev)
    {
        var nv = ev.NewValue;
        double? num = null;
        string? str = null;
        string? unit = null;

        if (nv is not null && nv.Type is not (null or "" or "Undefined"))
        {
            if (nv.Type == "Number")
            {
                num = nv.NumericValue;
                unit = string.IsNullOrEmpty(nv.Unit) ? null : nv.Unit;
            }
            else if (nv.Type == "String")
            {
                str = nv.StringValue;
            }
        }

        return new MeasureHistoryPoint
        {
            Name = ev.Name,
            Timestamp = DateTime.UtcNow,
            NumericValue = num,
            StringValue = str,
            Unit = unit,
        };
    }
}