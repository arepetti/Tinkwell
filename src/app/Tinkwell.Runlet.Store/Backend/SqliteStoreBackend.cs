using System.Globalization;
using System.Runtime.CompilerServices;
using Microsoft.Data.Sqlite;

namespace Tinkwell.Runlet.Store.Backend;

/// <summary>
/// SQLite-backed <see cref="IStoreBackend"/> with WAL journaling, used
/// when the runlet is configured for persistent storage.
/// </summary>
internal sealed class SqliteStoreBackend : IStoreBackend
{
    private const string TimestampFormat = "O";

    private readonly SqliteConnection _connection;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public SqliteStoreBackend(string dbPath)
    {
        var dir = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        _connection = new SqliteConnection($"Data Source={dbPath}");
        _connection.Open();

        Initialize();
    }

    private void Initialize()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            PRAGMA journal_mode=WAL;

            CREATE TABLE IF NOT EXISTS store (
                bucket_id      TEXT NOT NULL,
                key_namespace  TEXT NOT NULL,
                key            TEXT NOT NULL,
                value          TEXT NOT NULL,
                created_at     TEXT NOT NULL,
                updated_at     TEXT NOT NULL,
                expires_at     TEXT,
                PRIMARY KEY (bucket_id, key_namespace, key)
            );

            CREATE INDEX IF NOT EXISTS ix_store_expires ON store (expires_at)
                WHERE expires_at IS NOT NULL;

            CREATE TABLE IF NOT EXISTS bucket_config (
                bucket_id     TEXT PRIMARY KEY,
                discoverable  INTEGER NOT NULL DEFAULT 1
            );
            """;
        cmd.ExecuteNonQuery();
    }

    public async Task<StoreEntry?> GetAsync(string bucketId, string keyNamespace, string key)
    {
        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            SELECT value, created_at, updated_at, expires_at
            FROM store
            WHERE bucket_id = @b AND key_namespace = @n AND key = @k
            """;
        cmd.Parameters.AddWithValue("@b", bucketId);
        cmd.Parameters.AddWithValue("@n", keyNamespace);
        cmd.Parameters.AddWithValue("@k", key);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            return null;
        }

        var entry = ReadEntry(bucketId, keyNamespace, key, reader);
        return entry.IsExpired ? null : entry;
    }

    public async Task<StoreEntry> SetAsync(
        string bucketId, string keyNamespace, string key, string value, TimeSpan? ttl)
    {
        var now = DateTime.UtcNow;
        var expiresAt = ttl.HasValue ? now + ttl.Value : (DateTime?)null;

        await _writeLock.WaitAsync();
        try
        {
            await using var cmd = _connection.CreateCommand();
            cmd.CommandText = """
                INSERT INTO store (bucket_id, key_namespace, key, value, created_at, updated_at, expires_at)
                VALUES (@b, @n, @k, @v, @c, @u, @e)
                ON CONFLICT (bucket_id, key_namespace, key) DO UPDATE SET
                    value = @v,
                    updated_at = @u,
                    expires_at = @e
                RETURNING created_at
                """;
            cmd.Parameters.AddWithValue("@b", bucketId);
            cmd.Parameters.AddWithValue("@n", keyNamespace);
            cmd.Parameters.AddWithValue("@k", key);
            cmd.Parameters.AddWithValue("@v", value);
            cmd.Parameters.AddWithValue("@c", FormatTimestamp(now));
            cmd.Parameters.AddWithValue("@u", FormatTimestamp(now));
            cmd.Parameters.AddWithValue("@e", expiresAt.HasValue ? FormatTimestamp(expiresAt.Value) : DBNull.Value);

            await using var reader = await cmd.ExecuteReaderAsync();
            await reader.ReadAsync();

            var createdAt = ParseTimestamp(reader.GetString(0));
            return new StoreEntry(bucketId, keyNamespace, key, value, createdAt, now, expiresAt);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task<IReadOnlyList<StoreEntry>> SetManyAsync(
        IReadOnlyList<(string BucketId, string KeyNamespace, string Key, string Value, TimeSpan? Ttl)> entries)
    {
        var now = DateTime.UtcNow;

        await _writeLock.WaitAsync();
        try
        {
            await using var tx = _connection.BeginTransaction();
            var results = new List<StoreEntry>(entries.Count);

            foreach (var (bucketId, keyNamespace, key, value, ttl) in entries)
            {
                var expiresAt = ttl.HasValue ? now + ttl.Value : (DateTime?)null;

                await using var cmd = _connection.CreateCommand();
                cmd.Transaction = tx;
                cmd.CommandText = """
                    INSERT INTO store (bucket_id, key_namespace, key, value, created_at, updated_at, expires_at)
                    VALUES (@b, @n, @k, @v, @c, @u, @e)
                    ON CONFLICT (bucket_id, key_namespace, key) DO UPDATE SET
                        value = @v,
                        updated_at = @u,
                        expires_at = @e
                    RETURNING created_at
                    """;
                cmd.Parameters.AddWithValue("@b", bucketId);
                cmd.Parameters.AddWithValue("@n", keyNamespace);
                cmd.Parameters.AddWithValue("@k", key);
                cmd.Parameters.AddWithValue("@v", value);
                cmd.Parameters.AddWithValue("@c", FormatTimestamp(now));
                cmd.Parameters.AddWithValue("@u", FormatTimestamp(now));
                cmd.Parameters.AddWithValue("@e", expiresAt.HasValue
                    ? FormatTimestamp(expiresAt.Value) : DBNull.Value);

                await using var reader = await cmd.ExecuteReaderAsync();
                await reader.ReadAsync();

                var createdAt = ParseTimestamp(reader.GetString(0));
                results.Add(new StoreEntry(bucketId, keyNamespace, key, value, createdAt, now, expiresAt));
            }

            tx.Commit();
            return results;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task<bool> DeleteAsync(string bucketId, string keyNamespace, string key)
    {
        await _writeLock.WaitAsync();
        try
        {
            await using var cmd = _connection.CreateCommand();
            cmd.CommandText = """
                DELETE FROM store
                WHERE bucket_id = @b AND key_namespace = @n AND key = @k
                """;
            cmd.Parameters.AddWithValue("@b", bucketId);
            cmd.Parameters.AddWithValue("@n", keyNamespace);
            cmd.Parameters.AddWithValue("@k", key);

            return await cmd.ExecuteNonQueryAsync() > 0;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async IAsyncEnumerable<StoreEntry> ListAsync(
        string? bucketId, string? keyNamespace, string? prefix, bool includeHidden,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await using var cmd = _connection.CreateCommand();

        var where = new List<string>();
        var now = DateTime.UtcNow;

        where.Add("(s.expires_at IS NULL OR s.expires_at > @now)");
        cmd.Parameters.AddWithValue("@now", FormatTimestamp(now));

        if (!string.IsNullOrEmpty(bucketId))
        {
            where.Add("s.bucket_id = @b");
            cmd.Parameters.AddWithValue("@b", bucketId);
        }
        else if (!includeHidden)
        {
            where.Add("""
                NOT EXISTS (
                    SELECT 1 FROM bucket_config bc
                    WHERE bc.bucket_id = s.bucket_id AND bc.discoverable = 0
                )
                """);
        }

        if (!string.IsNullOrEmpty(keyNamespace))
        {
            where.Add("s.key_namespace = @n");
            cmd.Parameters.AddWithValue("@n", keyNamespace);
        }

        if (!string.IsNullOrEmpty(prefix))
        {
            where.Add("s.key LIKE @p || '%' ESCAPE '\\'");
            cmd.Parameters.AddWithValue("@p", EscapeLike(prefix));
        }

        cmd.CommandText = $"""
            SELECT s.bucket_id, s.key_namespace, s.key, s.value,
                   s.created_at, s.updated_at, s.expires_at
            FROM store s
            WHERE {string.Join(" AND ", where)}
            ORDER BY s.bucket_id, s.key_namespace, s.key
            """;

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            yield return new StoreEntry(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                ParseTimestamp(reader.GetString(4)),
                ParseTimestamp(reader.GetString(5)),
                reader.IsDBNull(6) ? null : ParseTimestamp(reader.GetString(6)));
        }
    }

    public async Task<IReadOnlyList<StoreEntry>> CleanupExpiredAsync()
    {
        await _writeLock.WaitAsync();
        try
        {
            await using var cmd = _connection.CreateCommand();
            cmd.CommandText = """
                DELETE FROM store
                WHERE expires_at IS NOT NULL AND expires_at <= @now
                RETURNING bucket_id, key_namespace, key, value, created_at, updated_at, expires_at
                """;
            cmd.Parameters.AddWithValue("@now", FormatTimestamp(DateTime.UtcNow));

            var expired = new List<StoreEntry>();
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                expired.Add(new StoreEntry(
                    reader.GetString(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetString(3),
                    ParseTimestamp(reader.GetString(4)),
                    ParseTimestamp(reader.GetString(5)),
                    reader.IsDBNull(6) ? null : ParseTimestamp(reader.GetString(6))));
            }

            return expired;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task SetBucketConfigAsync(BucketConfig config)
    {
        await _writeLock.WaitAsync();
        try
        {
            await using var cmd = _connection.CreateCommand();
            cmd.CommandText = """
                INSERT INTO bucket_config (bucket_id, discoverable)
                VALUES (@b, @d)
                ON CONFLICT (bucket_id) DO UPDATE SET discoverable = @d
                """;
            cmd.Parameters.AddWithValue("@b", config.BucketId);
            cmd.Parameters.AddWithValue("@d", config.Discoverable ? 1 : 0);
            await cmd.ExecuteNonQueryAsync();
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task<BucketConfig?> GetBucketConfigAsync(string bucketId)
    {
        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT discoverable FROM bucket_config WHERE bucket_id = @b";
        cmd.Parameters.AddWithValue("@b", bucketId);

        var result = await cmd.ExecuteScalarAsync();
        if (result is null or DBNull)
        {
            return null;
        }

        return new BucketConfig(bucketId, Convert.ToInt32(result, CultureInfo.InvariantCulture) != 0);
    }

    public async Task<IReadOnlySet<string>> GetHiddenBucketIdsAsync()
    {
        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT bucket_id FROM bucket_config WHERE discoverable = 0";

        var set = new HashSet<string>(StringComparer.Ordinal);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            set.Add(reader.GetString(0));
        }

        return set;
    }

    public async Task ClearAsync()
    {
        await _writeLock.WaitAsync();
        try
        {
            await using var cmd = _connection.CreateCommand();
            cmd.CommandText = "DELETE FROM store; DELETE FROM bucket_config;";
            await cmd.ExecuteNonQueryAsync();
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _connection.DisposeAsync();
        _writeLock.Dispose();
    }

    private StoreEntry ReadEntry(string bucketId, string keyNamespace, string key, SqliteDataReader reader) =>
        new(bucketId, keyNamespace, key,
            reader.GetString(0),
            ParseTimestamp(reader.GetString(1)),
            ParseTimestamp(reader.GetString(2)),
            reader.IsDBNull(3) ? null : ParseTimestamp(reader.GetString(3)));

    private static string FormatTimestamp(DateTime dt) =>
        dt.ToString(TimestampFormat, CultureInfo.InvariantCulture);

    private static DateTime ParseTimestamp(string s) =>
        DateTime.Parse(s, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

    private static string EscapeLike(string value) =>
        value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
}
