using Npgsql;

namespace Tinkwell.Measures.History.TimescaleDb;

internal static class SchemaManager
{
    public static async Task EnsureSchemaAsync(NpgsqlDataSource dataSource, CancellationToken ct)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct).ConfigureAwait(false);

        await ExecuteAsync(conn, """
            CREATE TABLE IF NOT EXISTS measure_definitions (
                name          TEXT PRIMARY KEY,
                type          TEXT NOT NULL,
                quantity_type TEXT,
                unit          TEXT,
                minimum       DOUBLE PRECISION,
                maximum       DOUBLE PRECISION,
                "precision"   INTEGER,
                description   TEXT,
                category      TEXT,
                tags          TEXT[],
                updated_at    TIMESTAMPTZ NOT NULL DEFAULT NOW()
            );
            """, ct).ConfigureAwait(false);

        await ExecuteAsync(conn, """
            CREATE TABLE IF NOT EXISTS measure_history (
                time           TIMESTAMPTZ    NOT NULL,
                name           TEXT           NOT NULL,
                numeric_value  DOUBLE PRECISION,
                string_value   TEXT,
                opaque_value   BYTEA,
                unit           TEXT,
                correlation_id TEXT
            );
            """, ct).ConfigureAwait(false);

        await ExecuteAsync(conn, """
            SELECT create_hypertable('measure_history', by_range('time'), if_not_exists => true);
            """, ct).ConfigureAwait(false);

        await ExecuteAsync(conn, """
            CREATE INDEX IF NOT EXISTS ix_measure_history_name_time ON measure_history (name, time DESC);
            """, ct).ConfigureAwait(false);
    }

    private static async Task ExecuteAsync(NpgsqlConnection conn, string sql, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand(sql, conn);
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }
}
