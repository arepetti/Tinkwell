using System.Globalization;
using Microsoft.Data.Sqlite;

namespace Tinkwell.Runlet.Store.Backend;

/// <summary>
/// Seeds an <see cref="IStoreBackend"/> from a SQLite database file.
/// The database is opened read-only and is never modified.
/// </summary>
internal static class StoreSeeder
{
    private const string TimestampFormat = "O";

    public static async Task SeedAsync(IStoreBackend backend, string dbPath)
    {
        if (!File.Exists(dbPath))
        {
            return;
        }

        await using var connection = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly");
        connection.Open();

        await SeedEntriesAsync(backend, connection);
        await SeedBucketConfigsAsync(backend, connection);
    }

    private static async Task SeedEntriesAsync(IStoreBackend backend, SqliteConnection connection)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT bucket_id, key_namespace, key, value, created_at, updated_at, expires_at
            FROM store
            WHERE expires_at IS NULL OR expires_at > @now
            """;
        cmd.Parameters.AddWithValue("@now", DateTime.UtcNow.ToString(TimestampFormat, CultureInfo.InvariantCulture));

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var bucketId = reader.GetString(0);
            var keyNamespace = reader.GetString(1);
            var key = reader.GetString(2);
            var value = reader.GetString(3);

            var expiresAt = reader.IsDBNull(6)
                ? (DateTime?)null
                : DateTime.Parse(reader.GetString(6), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

            TimeSpan? ttl = expiresAt.HasValue
                ? expiresAt.Value - DateTime.UtcNow
                : null;

            if (ttl is { Ticks: <= 0 })
            {
                continue;
            }

            await backend.SetAsync(bucketId, keyNamespace, key, value, ttl);
        }
    }

    private static async Task SeedBucketConfigsAsync(IStoreBackend backend, SqliteConnection connection)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT bucket_id, discoverable FROM bucket_config";

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var bucketId = reader.GetString(0);
            var discoverable = reader.GetInt32(1) != 0;
            await backend.SetBucketConfigAsync(new BucketConfig(bucketId, discoverable));
        }
    }
}
