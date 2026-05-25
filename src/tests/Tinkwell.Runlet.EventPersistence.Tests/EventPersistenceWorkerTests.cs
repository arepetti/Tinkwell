using System.Globalization;
using System.Threading.Channels;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Tinkwell.Events;
using Tinkwell.Runlet.EventPersistence;
using Tinkwell.Runlet.Events;

namespace Tinkwell.Runlet.EventPersistence.Tests;

public class EventPersistenceWorkerTests
{
    private static EventFanOut CreateFanOut(int capacity = 100) =>
        new(new EventFanOutConfig(new ChannelConfig(capacity, BoundedChannelFullMode.DropOldest)),
            NullLogger<EventFanOut>.Instance);

    private static EventEnvelope MakeEvent(string name) => new()
    {
        Source = "tests",
        Verb = EventVerb.Changed,
        Name = name,
        Payload = new Dictionary<string, string> { ["k"] = "v" },
    };

    private static EventPersistenceOptions MakeOptions(string dbPath, int batchSize = 10, int flushIntervalMs = 50) =>
        new(dbPath, batchSize, TimeSpan.FromMilliseconds(flushIntervalMs));

    // FillBufferAsync registers the subscriber lazily; wait until EventFanOut
    // actually sees at least one subscriber before publishing real events.
    private static async Task WaitForSubscriberAsync(EventFanOut fanOut, int timeoutMs = 2000)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            if (fanOut.SubscriberCount > 0)
                return;
            await Task.Delay(25);
        }

        throw new TimeoutException("EventFanOut did not register a subscriber in time.");
    }

    [Fact]
    public async Task RoundTrip_PersistsEventsToSqlite()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"tw-ep-{Guid.NewGuid():N}.db");
        try
        {
            var fanOut = CreateFanOut();
            var worker = new EventPersistenceWorker(fanOut, MakeOptions(dbPath),
                NullLogger<EventPersistenceWorker>.Instance);

            await worker.StartAsync(CancellationToken.None);
            await WaitForSubscriberAsync(fanOut);

            const int count = 5;
            for (int i=0; i < count; ++i)
                fanOut.Publish(MakeEvent($"event-{i}"));

            await Task.Delay(300);
            await worker.StopAsync(CancellationToken.None);
            SqliteConnection.ClearAllPools();

            using var connection = new SqliteConnection($"Data Source={dbPath}");
            connection.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM events";
            var rows = Convert.ToInt32(cmd.ExecuteScalar());
            Assert.Equal(count, rows);

            cmd.CommandText = "SELECT source, verb, name, payload FROM events ORDER BY id ASC LIMIT 1";
            using var reader = cmd.ExecuteReader();
            Assert.True(reader.Read());
            Assert.Equal("tests", reader.GetString(0));
            Assert.Equal("Changed", reader.GetString(1));
            Assert.Equal("event-0", reader.GetString(2));
            Assert.False(reader.IsDBNull(3));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            TryDelete(dbPath);
        }
    }

    [Fact]
    public async Task SchemaInit_IsIdempotent()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"tw-ep-{Guid.NewGuid():N}.db");
        try
        {
            var fanOut1 = CreateFanOut();
            var worker1 = new EventPersistenceWorker(fanOut1, MakeOptions(dbPath),
                NullLogger<EventPersistenceWorker>.Instance);
            await worker1.StartAsync(CancellationToken.None);
            await WaitForSubscriberAsync(fanOut1);
            await worker1.StopAsync(CancellationToken.None);
            SqliteConnection.ClearAllPools();

            var fanOut2 = CreateFanOut();
            var worker2 = new EventPersistenceWorker(fanOut2, MakeOptions(dbPath),
                NullLogger<EventPersistenceWorker>.Instance);
            await worker2.StartAsync(CancellationToken.None);
            await WaitForSubscriberAsync(fanOut2);

            fanOut2.Publish(MakeEvent("after-reopen"));

            await Task.Delay(300);
            await worker2.StopAsync(CancellationToken.None);
            SqliteConnection.ClearAllPools();

            using var connection = new SqliteConnection($"Data Source={dbPath}");
            connection.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM events WHERE name = 'after-reopen'";
            var rows = Convert.ToInt32(cmd.ExecuteScalar());
            Assert.Equal(1, rows);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            TryDelete(dbPath);
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
            var wal = path + "-wal";
            if (File.Exists(wal))
                File.Delete(wal);
            var shm = path + "-shm";
            if (File.Exists(shm))
                File.Delete(shm);
        }
        catch
        {
        }
    }

    [Fact]
    public async Task WriteBatch_EmptyPayload_StoresNullPayloadColumn()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"tw-ep-{Guid.NewGuid():N}.db");
        try
        {
            var fanOut = CreateFanOut();
            var worker = new EventPersistenceWorker(fanOut, MakeOptions(dbPath),
                NullLogger<EventPersistenceWorker>.Instance);

            await worker.StartAsync(CancellationToken.None);
            await WaitForSubscriberAsync(fanOut);

            fanOut.Publish(new EventEnvelope
            {
                Source = "tests",
                Verb = EventVerb.Changed,
                Name = "no-payload",
                Payload = new Dictionary<string, string>(),
            });

            await Task.Delay(300);
            await worker.StopAsync(CancellationToken.None);
            SqliteConnection.ClearAllPools();

            using var connection = new SqliteConnection($"Data Source={dbPath}");
            connection.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT payload FROM events WHERE name = 'no-payload'";
            var scalar = cmd.ExecuteScalar();
            Assert.Equal(DBNull.Value, scalar);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            TryDelete(dbPath);
        }
    }

    [Fact]
    public async Task WriteBatch_AllEventEnvelopeFields_PersistedCorrectly()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"tw-ep-{Guid.NewGuid():N}.db");
        var ts = new DateTime(2024, 3, 10, 14, 30, 0, DateTimeKind.Utc);
        try
        {
            var fanOut = CreateFanOut();
            var worker = new EventPersistenceWorker(fanOut, MakeOptions(dbPath),
                NullLogger<EventPersistenceWorker>.Instance);

            await worker.StartAsync(CancellationToken.None);
            await WaitForSubscriberAsync(fanOut);

            fanOut.Publish(new EventEnvelope
            {
                Source = "full",
                Verb = EventVerb.Custom,
                CustomVerb = "domain.verb",
                Name = "entity",
                Object = "target-object",
                CorrelationId = "corr-42",
                Timestamp = ts,
                Payload = new Dictionary<string, string> { ["p"] = "q" },
            });

            await Task.Delay(300);
            await worker.StopAsync(CancellationToken.None);
            SqliteConnection.ClearAllPools();

            using var connection = new SqliteConnection($"Data Source={dbPath}");
            connection.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = """
                SELECT source, verb, custom_verb, name, object, correlation_id, timestamp, payload
                FROM events WHERE name = 'entity'
                """;
            using var reader = cmd.ExecuteReader();
            Assert.True(reader.Read());
            Assert.Equal("full", reader.GetString(0));
            Assert.Equal("Custom", reader.GetString(1));
            Assert.Equal("domain.verb", reader.GetString(2));
            Assert.Equal("entity", reader.GetString(3));
            Assert.Equal("target-object", reader.GetString(4));
            Assert.Equal("corr-42", reader.GetString(5));
            var roundTrip = DateTime.Parse(reader.GetString(6), CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind);
            Assert.Equal(ts, roundTrip);
            Assert.False(reader.IsDBNull(7));
            Assert.Contains("p", reader.GetString(7));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            TryDelete(dbPath);
        }
    }

    [Fact]
    public async Task PartialBatch_FlushesOnFlushInterval_WithoutFillingBatch()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"tw-ep-{Guid.NewGuid():N}.db");
        try
        {
            var fanOut = CreateFanOut(500);
            var worker = new EventPersistenceWorker(fanOut,
                MakeOptions(dbPath, batchSize: 100, flushIntervalMs: 200),
                NullLogger<EventPersistenceWorker>.Instance);

            await worker.StartAsync(CancellationToken.None);
            await WaitForSubscriberAsync(fanOut);

            for (int i=0; i < 2; ++i)
                fanOut.Publish(MakeEvent($"small-batch-{i}"));

            await Task.Delay(800);
            await worker.StopAsync(CancellationToken.None);
            SqliteConnection.ClearAllPools();

            using var connection = new SqliteConnection($"Data Source={dbPath}");
            connection.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM events WHERE name LIKE 'small-batch-%'";
            var rows = Convert.ToInt32(cmd.ExecuteScalar());
            Assert.Equal(2, rows);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            TryDelete(dbPath);
        }
    }

    [Fact]
    public async Task StopAsync_DrainsPendingEvents_OnGracefulShutdown()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"tw-ep-{Guid.NewGuid():N}.db");
        try
        {
            var fanOut = CreateFanOut(500);
            var worker = new EventPersistenceWorker(fanOut,
                MakeOptions(dbPath, batchSize: 100, flushIntervalMs: 60_000),
                NullLogger<EventPersistenceWorker>.Instance);

            await worker.StartAsync(CancellationToken.None);
            await WaitForSubscriberAsync(fanOut);

            const int count = 5;
            for (int i=0; i < count; ++i)
                fanOut.Publish(MakeEvent($"drain-{i}"));

            // Allow fan-out -> persistence buffer handoff; immediate Stop cancels FillBuffer
            // before the subscriber channel is fully drained, dropping unpublished events.
            await Task.Delay(200);

            await worker.StopAsync(CancellationToken.None);
            SqliteConnection.ClearAllPools();

            using var connection = new SqliteConnection($"Data Source={dbPath}");
            connection.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM events WHERE name LIKE 'drain-%'";
            var rows = Convert.ToInt32(cmd.ExecuteScalar());
            Assert.Equal(count, rows);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            TryDelete(dbPath);
        }
    }

    [Fact]
    public async Task BurstLargerThanBatchSize_WritesAllEvents()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"tw-ep-{Guid.NewGuid():N}.db");
        try
        {
            var fanOut = CreateFanOut(500);
            const int batch = 3;
            var worker = new EventPersistenceWorker(fanOut,
                MakeOptions(dbPath, batchSize: batch, flushIntervalMs: 60_000),
                NullLogger<EventPersistenceWorker>.Instance);

            await worker.StartAsync(CancellationToken.None);
            await WaitForSubscriberAsync(fanOut);

            const int total = 10;
            for (int i=0; i < total; ++i)
                fanOut.Publish(MakeEvent($"burst-{i}"));

            // See StopAsync_Drains — yield so FillBuffer can dequeue from the fan-out channel.
            await Task.Delay(200);

            await worker.StopAsync(CancellationToken.None);
            SqliteConnection.ClearAllPools();

            using var connection = new SqliteConnection($"Data Source={dbPath}");
            connection.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM events WHERE name LIKE 'burst-%'";
            var rows = Convert.ToInt32(cmd.ExecuteScalar());
            Assert.Equal(total, rows);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            TryDelete(dbPath);
        }
    }

    [Fact]
    public void OpenSqlite_PathIsExistingDirectory_Throws()
    {
        var dir = Path.Combine(Path.GetTempPath(), "tw-epdir-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            using var connection = new SqliteConnection($"Data Source={dir}");
            Assert.Throws<SqliteException>(() => connection.Open());
        }
        finally
        {
            try
            {
                Directory.Delete(dir);
            }
            catch
            {
            }
        }
    }
}
