using System.Text;
using Npgsql;
using NpgsqlTypes;

namespace Tinkwell.Measures.History.TimescaleDb;

/// <summary>
/// <see cref="IMeasureHistoryStore"/> implementation backed by TimescaleDB (PostgreSQL + Timescale extension).
/// </summary>
public sealed class TimescaleDbMeasureHistoryStore : IMeasureHistoryStore
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly TimescaleDbOptions _options;
    private readonly object _schemaLock = new();
    private Task? _schemaReady;

    /// <summary>Creates a store for the given options. Schema is applied on first use when <see cref="TimescaleDbOptions.AutoCreateSchema"/> is set.</summary>
    public TimescaleDbMeasureHistoryStore(TimescaleDbOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
        _dataSource = NpgsqlDataSource.Create(options.ConnectionString);
    }

    /// <summary>
    /// Creates a store with <see cref="TimescaleDbOptions.AutoCreateSchema"/> <see langword="true"/> (for runlet and host discovery via <c>(string)</c> constructor).
    /// </summary>
    /// <param name="connectionString">Npgsql connection string.</param>
    public TimescaleDbMeasureHistoryStore(string connectionString)
        : this(new TimescaleDbOptions
        {
            ConnectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString)),
            AutoCreateSchema = true,
        })
    {
    }

    /// <summary>
    /// Creates a store and ensures schema when <see cref="TimescaleDbOptions.AutoCreateSchema"/> is set.
    /// </summary>
    public static async Task<TimescaleDbMeasureHistoryStore> CreateAsync(TimescaleDbOptions options, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        var store = new TimescaleDbMeasureHistoryStore(options);
        await store.EnsureSchemaOnceAsync(ct).ConfigureAwait(false);
        return store;
    }

    private Task EnsureSchemaOnceAsync(CancellationToken ct)
    {
        if (!_options.AutoCreateSchema)
            return Task.CompletedTask;

        lock (_schemaLock)
        {
            if (_schemaReady is not null && !_schemaReady.IsFaulted && !_schemaReady.IsCanceled)
                return _schemaReady;

            _schemaReady = SchemaManager.EnsureSchemaAsync(_dataSource, ct);
        }

        return _schemaReady;
    }

    /// <inheritdoc />
    public Task WriteAsync(MeasureHistoryPoint point, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(point);
        return WriteManyAsync([point], ct);
    }

    /// <inheritdoc />
    public async Task WriteManyAsync(IReadOnlyList<MeasureHistoryPoint> points, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(points);
        if (points.Count == 0)
            return;

        await EnsureSchemaOnceAsync(ct).ConfigureAwait(false);
        await using var conn = await _dataSource.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var writer = await conn.BeginBinaryImportAsync(
            """
            COPY measure_history (time, name, numeric_value, string_value, opaque_value, unit, correlation_id)
            FROM STDIN (FORMAT BINARY)
            """,
            ct).ConfigureAwait(false);

        foreach (var point in points)
        {
            await writer.StartRowAsync(ct).ConfigureAwait(false);
            await writer.WriteAsync(NormalizeTimestamp(point.Timestamp), NpgsqlDbType.TimestampTz, ct).ConfigureAwait(false);
            await writer.WriteAsync(point.Name, NpgsqlDbType.Text, ct).ConfigureAwait(false);

            if (point.NumericValue is { } nv)
                await writer.WriteAsync(nv, NpgsqlDbType.Double, ct).ConfigureAwait(false);
            else
                await writer.WriteNullAsync(ct).ConfigureAwait(false);

            if (point.StringValue is { } sv)
                await writer.WriteAsync(sv, NpgsqlDbType.Text, ct).ConfigureAwait(false);
            else
                await writer.WriteNullAsync(ct).ConfigureAwait(false);

            if (point.OpaqueValue is { } ov)
                await writer.WriteAsync(ov, NpgsqlDbType.Bytea, ct).ConfigureAwait(false);
            else
                await writer.WriteNullAsync(ct).ConfigureAwait(false);

            if (point.Unit is { } u)
                await writer.WriteAsync(u, NpgsqlDbType.Text, ct).ConfigureAwait(false);
            else
                await writer.WriteNullAsync(ct).ConfigureAwait(false);

            if (point.CorrelationId is { } cid)
                await writer.WriteAsync(cid, NpgsqlDbType.Text, ct).ConfigureAwait(false);
            else
                await writer.WriteNullAsync(ct).ConfigureAwait(false);
        }

        await writer.CompleteAsync(ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<MeasureHistoryResult> QueryAsync(MeasureHistoryQuery query, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (query.Limit is <= 0)
            throw new ArgumentOutOfRangeException(nameof(query), "Limit must be positive when set.");

        await EnsureSchemaOnceAsync(ct).ConfigureAwait(false);

        if (query.Aggregation is null or HistoryAggregation.None)
            return await QueryRawAsync(query, ct).ConfigureAwait(false);

        if (query.AggregationInterval is null)
            throw new ArgumentException("AggregationInterval is required when Aggregation is set.", nameof(query));

        return await QueryAggregatedAsync(query, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task SyncDefinitionAsync(MeasureDefinitionSnapshot definition, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(definition);
        await EnsureSchemaOnceAsync(ct).ConfigureAwait(false);

        const string sql = """
            INSERT INTO measure_definitions (name, type, quantity_type, unit, minimum, maximum, "precision", description, category, tags, updated_at)
            VALUES (@name, @type, @quantity_type, @unit, @minimum, @maximum, @precision, @description, @category, @tags, NOW())
            ON CONFLICT (name) DO UPDATE SET
                type = EXCLUDED.type,
                quantity_type = EXCLUDED.quantity_type,
                unit = EXCLUDED.unit,
                minimum = EXCLUDED.minimum,
                maximum = EXCLUDED.maximum,
                "precision" = EXCLUDED."precision",
                description = EXCLUDED.description,
                category = EXCLUDED.category,
                tags = EXCLUDED.tags,
                updated_at = NOW()
            """;

        await using var conn = await _dataSource.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("name", definition.Name);
        cmd.Parameters.AddWithValue("type", definition.Type);
        cmd.Parameters.AddWithValue("quantity_type", (object?)definition.QuantityType ?? DBNull.Value);
        cmd.Parameters.AddWithValue("unit", (object?)definition.Unit ?? DBNull.Value);
        cmd.Parameters.AddWithValue("minimum", (object?)definition.Minimum ?? DBNull.Value);
        cmd.Parameters.AddWithValue("maximum", (object?)definition.Maximum ?? DBNull.Value);
        cmd.Parameters.AddWithValue("precision", (object?)definition.Precision ?? DBNull.Value);
        cmd.Parameters.AddWithValue("description", (object?)definition.Description ?? DBNull.Value);
        cmd.Parameters.AddWithValue("category", (object?)definition.Category ?? DBNull.Value);
        cmd.Parameters.Add(
            new NpgsqlParameter("tags", NpgsqlDbType.Array | NpgsqlDbType.Text)
            {
                Value = definition.Tags.Count == 0 ? Array.Empty<string>() : definition.Tags.ToArray(),
            });

        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<MeasureDefinitionSnapshot>> GetDefinitionsAsync(CancellationToken ct = default)
    {
        await EnsureSchemaOnceAsync(ct).ConfigureAwait(false);

        const string sql = """
            SELECT name, type, quantity_type, unit, minimum, maximum, "precision", description, category, tags
            FROM measure_definitions
            ORDER BY name
            """;

        var list = new List<MeasureDefinitionSnapshot>();
        await using var conn = await _dataSource.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = new NpgsqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            list.Add(new MeasureDefinitionSnapshot
            {
                Name = reader.GetString(0),
                Type = reader.GetString(1),
                QuantityType = reader.IsDBNull(2) ? null : reader.GetString(2),
                Unit = reader.IsDBNull(3) ? null : reader.GetString(3),
                Minimum = reader.IsDBNull(4) ? null : reader.GetDouble(4),
                Maximum = reader.IsDBNull(5) ? null : reader.GetDouble(5),
                Precision = reader.IsDBNull(6) ? null : reader.GetInt32(6),
                Description = reader.IsDBNull(7) ? null : reader.GetString(7),
                Category = reader.IsDBNull(8) ? null : reader.GetString(8),
                Tags = reader.IsDBNull(9) ? [] : reader.GetFieldValue<string[]>(9),
            });
        }

        return list;
    }

    /// <inheritdoc />
    public async Task<MeasureDataRange> GetDataRangeAsync(string name, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        await EnsureSchemaOnceAsync(ct).ConfigureAwait(false);

        const string sql = """
            SELECT MIN(time), MAX(time)
            FROM measure_history
            WHERE name = @name
            """;

        await using var conn = await _dataSource.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("name", name.Trim());
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);

        if (await reader.ReadAsync(ct).ConfigureAwait(false) && !reader.IsDBNull(0))
        {
            return new MeasureDataRange
            {
                Earliest = NormalizeFromDb(reader.GetDateTime(0)),
                Latest = NormalizeFromDb(reader.GetDateTime(1)),
            };
        }

        return new MeasureDataRange();
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await _dataSource.DisposeAsync().ConfigureAwait(false);
    }

    private async Task<MeasureHistoryResult> QueryRawAsync(MeasureHistoryQuery query, CancellationToken ct)
    {
        var sql = new StringBuilder(
            """
            SELECT time, name, numeric_value, string_value, opaque_value, unit, correlation_id
            FROM measure_history
            WHERE name = @name
            """);

        if (query.From is not null)
            sql.Append(" AND time >= @from");

        if (query.To is not null)
            sql.Append(" AND time < @to");

        sql.Append(" ORDER BY time DESC");

        if (query.Limit is { } lim)
            sql.Append(" LIMIT @lim");

        await using var conn = await _dataSource.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = new NpgsqlCommand(sql.ToString(), conn);
        cmd.Parameters.AddWithValue("name", query.Name);
        if (query.From is { } from)
            cmd.Parameters.AddWithValue("from", NormalizeTimestamp(from));
        if (query.To is { } to)
            cmd.Parameters.AddWithValue("to", NormalizeTimestamp(to));
        if (query.Limit is { } limVal)
            cmd.Parameters.AddWithValue("lim", limVal + 1);

        var points = new List<MeasureHistoryPoint>();
        await using (var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false))
        {
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                points.Add(ReadPoint(reader));
            }
        }

        return TrimToLimit(query, points);
    }

    private static MeasureHistoryPoint ReadPoint(NpgsqlDataReader reader) => new()
    {
        Timestamp = NormalizeFromDb(reader.GetDateTime(0)),
        Name = reader.GetString(1),
        NumericValue = reader.IsDBNull(2) ? null : reader.GetDouble(2),
        StringValue = reader.IsDBNull(3) ? null : reader.GetString(3),
        OpaqueValue = reader.IsDBNull(4) ? null : reader.GetFieldValue<byte[]>(4),
        Unit = reader.IsDBNull(5) ? null : reader.GetString(5),
        CorrelationId = reader.IsDBNull(6) ? null : reader.GetString(6),
    };

    private async Task<MeasureHistoryResult> QueryAggregatedAsync(MeasureHistoryQuery query, CancellationToken ct)
    {
        var aggExpr = query.Aggregation!.Value switch
        {
            HistoryAggregation.Average => "avg(numeric_value)",
            HistoryAggregation.Min => "min(numeric_value)",
            HistoryAggregation.Max => "max(numeric_value)",
            HistoryAggregation.Sum => "sum(numeric_value)",
            HistoryAggregation.Count => "count(*)::float8",
            HistoryAggregation.First => "first(numeric_value, time)",
            HistoryAggregation.Last => "last(numeric_value, time)",
            HistoryAggregation.None => throw new InvalidOperationException("Aggregation must not be None in aggregated query."),
            _ => throw new ArgumentOutOfRangeException(nameof(query), query.Aggregation, "Unknown aggregation."),
        };

        var sql = new StringBuilder(
            $"""
            SELECT time_bucket(@interval, time) AS bucket, {aggExpr} AS numeric_value
            FROM measure_history
            WHERE name = @name
            """);

        if (query.From is not null)
            sql.Append(" AND time >= @from");

        if (query.To is not null)
            sql.Append(" AND time < @to");

        sql.Append(" GROUP BY bucket ORDER BY bucket DESC");

        if (query.Limit is not null)
            sql.Append(" LIMIT @lim");

        await using var conn = await _dataSource.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = new NpgsqlCommand(sql.ToString(), conn);
        cmd.Parameters.AddWithValue("interval", query.AggregationInterval!.Value);
        cmd.Parameters.AddWithValue("name", query.Name);
        if (query.From is { } from)
            cmd.Parameters.AddWithValue("from", NormalizeTimestamp(from));
        if (query.To is { } to)
            cmd.Parameters.AddWithValue("to", NormalizeTimestamp(to));
        if (query.Limit is { } limVal)
            cmd.Parameters.AddWithValue("lim", limVal + 1);

        var points = new List<MeasureHistoryPoint>();
        await using (var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false))
        {
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                var bucket = NormalizeFromDb(reader.GetDateTime(0));
                points.Add(
                    new MeasureHistoryPoint
                    {
                        Name = query.Name,
                        Timestamp = bucket,
                        NumericValue = reader.IsDBNull(1) ? null : reader.GetDouble(1),
                    });
            }
        }

        return TrimToLimit(query, points);
    }

    private static MeasureHistoryResult TrimToLimit(MeasureHistoryQuery query, List<MeasureHistoryPoint> points)
    {
        if (query.Limit is null)
            return new MeasureHistoryResult { Points = points, HasMore = false };

        var hasMore = points.Count > query.Limit.Value;
        if (hasMore)
            points.RemoveAt(points.Count - 1);

        return new MeasureHistoryResult { Points = points, HasMore = hasMore };
    }

    private static DateTime NormalizeTimestamp(DateTime timestamp) =>
        timestamp.Kind switch
        {
            DateTimeKind.Utc => timestamp,
            DateTimeKind.Local => timestamp.ToUniversalTime(),
            _ => DateTime.SpecifyKind(timestamp, DateTimeKind.Utc),
        };

    private static DateTime NormalizeFromDb(DateTime timestamp) =>
        timestamp.Kind == DateTimeKind.Utc
            ? timestamp
            : DateTime.SpecifyKind(timestamp.ToUniversalTime(), DateTimeKind.Utc);
}
