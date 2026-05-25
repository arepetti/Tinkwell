using Tinkwell.Measures.History;

namespace Tinkwell.Runlet.MeasureHistory.Tests;

/// <summary>
/// In-memory <see cref="IMeasureHistoryStore"/> for tests; mirrors production query/limit semantics loosely.
/// </summary>
internal sealed class FakeMeasureHistoryStore : IMeasureHistoryStore
{
    public List<MeasureHistoryPoint> WrittenPoints { get; } = [];
    public List<MeasureDefinitionSnapshot> Definitions { get; } = [];

    /// <summary>Last query passed to <see cref="QueryAsync"/> for mapping/assertion helpers.</summary>
    public MeasureHistoryQuery? LastQuery { get; private set; }

    public Task WriteAsync(MeasureHistoryPoint point, CancellationToken ct = default)
    {
        WrittenPoints.Add(point);
        return Task.CompletedTask;
    }

    public Task WriteManyAsync(IReadOnlyList<MeasureHistoryPoint> points, CancellationToken ct = default)
    {
        WrittenPoints.AddRange(points);
        return Task.CompletedTask;
    }

    public Task<MeasureHistoryResult> QueryAsync(MeasureHistoryQuery query, CancellationToken ct = default)
    {
        LastQuery = query;
        var filtered = WrittenPoints
            .Where(p => p.Name == query.Name)
            .Where(p => query.From is null || p.Timestamp >= query.From)
            .Where(p => query.To is null || p.Timestamp < query.To)
            .OrderByDescending(p => p.Timestamp)
            .ToList();

        var hasMore = false;
        if (query.Limit is { } take && filtered.Count > take)
        {
            hasMore = true;
            filtered = filtered.Take(take).ToList();
        }

        return Task.FromResult(new MeasureHistoryResult { Points = filtered, HasMore = hasMore });
    }

    public Task SyncDefinitionAsync(MeasureDefinitionSnapshot definition, CancellationToken ct = default)
    {
        Definitions.RemoveAll(d => d.Name == definition.Name);
        Definitions.Add(definition);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<MeasureDefinitionSnapshot>> GetDefinitionsAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<MeasureDefinitionSnapshot>>(Definitions.ToList());

    public Task<MeasureDataRange> GetDataRangeAsync(string name, CancellationToken ct = default)
    {
        var matching = WrittenPoints.Where(p => p.Name == name).ToList();
        if (matching.Count == 0)
            return Task.FromResult(new MeasureDataRange());

        return Task.FromResult(new MeasureDataRange
        {
            Earliest = matching.Min(p => p.Timestamp),
            Latest = matching.Max(p => p.Timestamp),
        });
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

/// <summary>Throws from <see cref="QueryAsync"/> for error-path coverage; other members are no-ops.</summary>
internal sealed class ThrowingQueryMeasureHistoryStore : IMeasureHistoryStore
{
    public Exception Exception { get; init; } = new InvalidOperationException("Query failed");

    public Task WriteAsync(MeasureHistoryPoint point, CancellationToken ct = default) => Task.CompletedTask;

    public Task WriteManyAsync(IReadOnlyList<MeasureHistoryPoint> points, CancellationToken ct = default) =>
        Task.CompletedTask;

    public Task<MeasureHistoryResult> QueryAsync(MeasureHistoryQuery query, CancellationToken ct = default) =>
        throw Exception;

    public Task SyncDefinitionAsync(MeasureDefinitionSnapshot definition, CancellationToken ct = default) =>
        Task.CompletedTask;

    public Task<IReadOnlyList<MeasureDefinitionSnapshot>> GetDefinitionsAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<MeasureDefinitionSnapshot>>([]);

    public Task<MeasureDataRange> GetDataRangeAsync(string name, CancellationToken ct = default) =>
        throw Exception;

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
