using System.Text.Json;
using System.Threading.Channels;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Tinkwell.Events;
using Tinkwell.Runlet.Events;

namespace Tinkwell.Runlet.EventPersistence;

/// <summary>
/// Subscribes to the in-process <see cref="Tinkwell.Runlet.Events.EventFanOut"/>, stages events
/// in an intermediate bounded channel, and writes batches to SQLite.
/// </summary>
/// <remarks>
/// <para><b>Batching.</b> A background task copies every matching event from
/// <c>SubscribeAsync</c> into a bounded channel
/// (capacity <c>BatchSize * 4</c>, <see cref="BoundedChannelFullMode.Wait"/>).
/// The main loop accumulates a batch until either
/// <see cref="EventPersistenceOptions.BatchSize"/> is reached or
/// <see cref="EventPersistenceOptions.FlushInterval"/> elapses, then issues
/// one transaction with one <c>INSERT</c> per event. On shutdown, remaining
/// channel items are drained and written.
/// </para>
/// <para><b>SQLite.</b> The connection enables WAL (<c>PRAGMA journal_mode=WAL</c>)
/// and creates the <c>events</c> table and indexes on first open.
/// <see cref="Tinkwell.Events.EventEnvelope.Payload"/> is serialized to JSON
/// when non-empty; scalar fields map to table columns.
/// </para>
/// <para><b>Persistence delivery.</b> If a batch write fails, the error is
/// logged and the batch is retained for one retry after the next flush interval
/// elapses. If the retry also fails, the batch is dropped with a warning.
/// Upstream, this worker is an ordinary bus subscriber, so when the
/// <c>events</c> runlet's subscriber channel is full, events can be dropped per
/// that runlet's <c>subscriber-channel-full-mode</c> before they reach this
/// worker.
/// </para>
/// </remarks>
internal sealed class EventPersistenceWorker : BackgroundService
{
    private readonly EventFanOut _fanOut;
    private readonly EventPersistenceOptions _options;
    private readonly ILogger<EventPersistenceWorker> _logger;

    private SqliteConnection? _connection;

    public EventPersistenceWorker(
        EventFanOut fanOut,
        EventPersistenceOptions options,
        ILogger<EventPersistenceWorker> logger)
    {
        _fanOut = fanOut;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _connection = InitializeDatabase();

        try
        {
            _logger.LogInformation("Event persistence started, writing to {DbPath}", _options.DbPath);

            var buffer = Channel.CreateBounded<EventEnvelope>(
                new BoundedChannelOptions(_options.BatchSize * 4)
                {
                    FullMode = BoundedChannelFullMode.Wait
                });

            var readerTask = FillBufferAsync(buffer.Writer, stoppingToken);

            var batch = new List<EventEnvelope>(_options.BatchSize);
            var isRetry = false;

            try
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    if (isRetry)
                    {
                        await Task.Delay(_options.FlushInterval, stoppingToken);
                    }
                    else
                    {
                        using var flushCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                        flushCts.CancelAfter(_options.FlushInterval);

                        try
                        {
                            while (batch.Count < _options.BatchSize)
                                batch.Add(await buffer.Reader.ReadAsync(flushCts.Token));
                        }
                        catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
                        {
                            // Flush interval elapsed — flush partial batch
                        }
                        catch (ChannelClosedException)
                        {
                            break;
                        }
                    }

                    if (batch.Count > 0)
                    {
                        if (WriteBatch(batch))
                        {
                            batch.Clear();
                            isRetry = false;
                        }
                        else if (isRetry)
                        {
                            _logger.LogWarning("Retry failed, dropping {Count} event(s)", batch.Count);
                            batch.Clear();
                            isRetry = false;
                        }
                        else
                        {
                            isRetry = true;
                        }
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Graceful shutdown
            }

            while (buffer.Reader.TryRead(out var remaining))
                batch.Add(remaining);

            if (batch.Count > 0 && !WriteBatch(batch))
                WriteBatch(batch);

            await readerTask;

            _logger.LogInformation("Event persistence stopped");
        }
        finally
        {
            _connection?.Dispose();
            _connection = null;
        }
    }

    private async Task FillBufferAsync(ChannelWriter<EventEnvelope> writer, CancellationToken ct)
    {
        try
        {
            await foreach (var e in _fanOut.SubscribeAsync(new SubscribeFilter(), ct))
                await writer.WriteAsync(e, ct);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            writer.TryComplete();
        }
    }

    private SqliteConnection InitializeDatabase()
    {
        var dir = Path.GetDirectoryName(_options.DbPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var connection = new SqliteConnection($"Data Source={_options.DbPath}");
        try
        {
            connection.Open();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = """
                PRAGMA journal_mode=WAL;

                CREATE TABLE IF NOT EXISTS events (
                    id             INTEGER PRIMARY KEY AUTOINCREMENT,
                    source         TEXT NOT NULL,
                    verb           TEXT NOT NULL,
                    custom_verb    TEXT,
                    name           TEXT NOT NULL,
                    object         TEXT,
                    correlation_id TEXT,
                    timestamp      TEXT NOT NULL,
                    payload        TEXT
                );

                CREATE INDEX IF NOT EXISTS ix_events_timestamp ON events (timestamp);
                CREATE INDEX IF NOT EXISTS ix_events_name ON events (name);
                CREATE INDEX IF NOT EXISTS ix_events_source ON events (source);
                """;
            cmd.ExecuteNonQuery();

            return connection;
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }

    private bool WriteBatch(List<EventEnvelope> batch)
    {
        if (_connection is null)
            return false;

        try
        {
            using var tx = _connection.BeginTransaction();
            using var cmd = _connection.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = """
                INSERT INTO events (source, verb, custom_verb, name, object,
                                    correlation_id, timestamp, payload)
                VALUES (@source, @verb, @customVerb, @name, @object,
                        @correlationId, @timestamp, @payload)
                """;
            cmd.Parameters.AddWithValue("@source", "");
            cmd.Parameters.AddWithValue("@verb", "");
            cmd.Parameters.AddWithValue("@customVerb", DBNull.Value);
            cmd.Parameters.AddWithValue("@name", "");
            cmd.Parameters.AddWithValue("@object", DBNull.Value);
            cmd.Parameters.AddWithValue("@correlationId", DBNull.Value);
            cmd.Parameters.AddWithValue("@timestamp", "");
            cmd.Parameters.AddWithValue("@payload", DBNull.Value);

            foreach (var e in batch)
            {
                cmd.Parameters["@source"].Value = e.Source;
                cmd.Parameters["@verb"].Value = e.Verb.ToString();
                cmd.Parameters["@customVerb"].Value = (object?)e.CustomVerb ?? DBNull.Value;
                cmd.Parameters["@name"].Value = e.Name;
                cmd.Parameters["@object"].Value = (object?)e.Object ?? DBNull.Value;
                cmd.Parameters["@correlationId"].Value = (object?)e.CorrelationId ?? DBNull.Value;
                cmd.Parameters["@timestamp"].Value = e.Timestamp.ToString("O", System.Globalization.CultureInfo.InvariantCulture);
                cmd.Parameters["@payload"].Value = e.Payload.Count > 0
                    ? JsonSerializer.Serialize(e.Payload)
                    : DBNull.Value;

                cmd.ExecuteNonQuery();
            }

            tx.Commit();

            _logger.LogDebug("Persisted {Count} event(s)", batch.Count);
            return true;
        }
        catch (OutOfMemoryException) { Environment.FailFast("Out of memory"); throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist {Count} event(s)", batch.Count);
            return false;
        }
    }
}