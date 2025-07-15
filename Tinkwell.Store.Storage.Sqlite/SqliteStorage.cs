using Dapper;
using Microsoft.Data.Sqlite;
using System.Collections.Concurrent;
using System.Data;
using Tinkwell.Measures;
using Tinkwell.Measures.Storage;

namespace Tinkwell.Store.Storage.Sqlite;

sealed class SqliteStorage : IStorage
{
    public SqliteStorage(SqliteConnection connection)
    {
        _connection = connection;
        if (connection.State == ConnectionState.Closed)
            _connection.Open();

        InitializeDatabase();
    }

    public event EventHandler<ValueChangedEventArgs>? ValueChanged;

    public bool SupportsTransactions => true;

    public IStorageTransaction BeginTransaction()
        => new SqliteStorageTransaction(this, _connection.BeginTransaction());

    public async ValueTask<bool> RegisterAsync(MeasureDefinition definition, MeasureMetadata metadata, CancellationToken cancellationToken)
        => await RegisterImplAsync(definition, metadata, null);

    public async ValueTask DeregisterAsync(string name, CancellationToken cancellationToken)
        => await DeregisterImplAsync(name, null);

    public async ValueTask<Measure> UpdateAsync(string name, MeasureValue value, CancellationToken cancellationToken)
        => await UpdateImplAsync(name, value, cancellationToken, null);

    public async ValueTask<IEnumerable<Measure>> FindAllAsync(CancellationToken cancellationToken)
        => await FindAllImplAsync(cancellationToken, null);

    public async ValueTask<IEnumerable<Measure>> FindAllAsync(IEnumerable<string> names, CancellationToken cancellationToken)
        => await FindAllImplAsync(names, cancellationToken, null);

    public Measure? Find(string name)
    {
        // This should be always the case, values should be available from cache
        // unless the DB is too big and we started to discard them.
        if (_measureCache.TryGetValue(name, out var measure))
            return measure;

        return FindImplAsync(name, null).GetAwaiter().GetResult();
    }

    public MeasureDefinition? FindDefinition(string name)
    {
        // This should be always the case, values should be available from cache
        // unless the DB is too big and we started to discard them.
        if (_measureCache.TryGetValue(name, out var measure))
            return measure.Definition;

        return FindDefinitionImplAsync(name, null).GetAwaiter().GetResult();
    }

    public async ValueTask<IEnumerable<MeasureDefinition>> FindAllDefinitionsAsync(CancellationToken cancellationToken)
        => await _connection.QueryAsync<MeasureDefinition>(SqlCommands.FindAllDefinitions);

    public async ValueTask<IEnumerable<MeasureDefinition>> FindAllDefinitionsAsync(IEnumerable<string> names, CancellationToken cancellationToken)
        => await _connection.QueryAsync<MeasureDefinition>(SqlCommands.FindAllDefinitionsByNames, new { Names = names });

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    internal async Task<bool> RegisterImplAsync(MeasureDefinition definition, MeasureMetadata metadata, IDbTransaction? transaction)
    {
        // If this measure has been already added during this session then it's definitely an error
        if (_sessionRegisteredMeasures.TryGetValue(definition.Name, out var _))
            return false;

        // Measures are expected to exist, it's OK. TODO: we should update the record with the new values
        // (in case they changed) or keep track of them all for historical reasons.
        if (FindDefinition(definition.Name) is not null)
            return true;

        await _connection.ExecuteAsync(SqlCommands.InsertDefinition, definition, transaction);
        await _connection.ExecuteAsync(SqlCommands.InsertMetadata, new { definition.Name, CreatedAt = ((DateTimeOffset)metadata.CreatedAt).ToUnixTimeMilliseconds(), metadata.Description, metadata.Category, Tags = string.Join(",", metadata.Tags) }, transaction);
        await _connection.ExecuteAsync(SqlCommands.InsertMeasure, new { definition.Name, Type = (int)MeasureValueType.Undefined }, transaction);

        _sessionRegisteredMeasures.Add(definition.Name);
        _measureCache.TryAdd(definition.Name, new Measure(definition, metadata, MeasureValue.Undefined));

        return true;
    }

    internal async Task DeregisterImplAsync(string name, IDbTransaction? transaction)
    {
        if (_sessionRegisteredMeasures.Contains(name))
        {
            await _connection.ExecuteAsync(SqlCommands.DeleteMeasure, new { Name = name }, transaction);
            await _connection.ExecuteAsync(SqlCommands.DeleteMetadata, new { Name = name }, transaction);
            await _connection.ExecuteAsync(SqlCommands.DeleteDefinition, new { Name = name }, transaction);
            _sessionRegisteredMeasures.Remove(name);
            _measureCache.TryRemove(name, out _);
        }
    }

    internal async Task<Measure> UpdateImplAsync(string name, MeasureValue value, CancellationToken cancellationToken, IDbTransaction? transaction)
    {
        // Here we can't use the faster Find() because we might have been called in the context of a transaction.
        var measure = await FindImplAsync(name, transaction);
        if (measure is null)
            throw new InvalidOperationException("Cannot add a new entry with a value. Use RegisterAsync() first.");

        var timestamp = ((DateTimeOffset)value.Timestamp).ToUnixTimeMilliseconds();
        int type = (int)value.Type;
        object parameters = new { Name = name, Timestamp = timestamp, Type = type, StringValue = (string?)null, DoubleValue = (double?)null };

        if (value.Type == MeasureValueType.String)
        {
            parameters = new { Name = name, Timestamp = timestamp, Type = type, StringValue = value.AsString(), DoubleValue = (double?)null };
        }
        else if (value.Type == MeasureValueType.Number)
        {
            var quantity = value.AsQuantity();
            parameters = new { Name = name, Timestamp = timestamp, Type = type, StringValue = (string?)null, DoubleValue = (double)quantity.Value };
        }

        await _connection.ExecuteAsync(SqlCommands.UpdateMeasureValue, parameters, transaction);
        _measureCache.AddOrUpdate(name, measure, (key, old) =>
        {
            if (old.Value != value)
            {
                // Raise the event on a thread pool thread to avoid blocking the update operation.
                _ = Task.Run(() => ValueChanged?.Invoke(this, new ValueChangedEventArgs(name, old.Value, value)));
            }
            return old with { Value = value };
        });

        return measure;
    }

    internal async ValueTask<IEnumerable<Measure>> FindAllImplAsync(CancellationToken cancellationToken, IDbTransaction? transaction)
    {
        var measures = await _connection.QueryAsync<MeasureDto>(SqlCommands.FindAllMeasures, transaction: transaction);
        var definitions = await _connection.QueryAsync<MeasureDefinition>(SqlCommands.FindAllDefinitions, transaction: transaction);
        var metadata = await _connection.QueryAsync<MeasureMetadataDto>(SqlCommands.FindAllMetadata, transaction: transaction);

        if (cancellationToken.IsCancellationRequested)
            return [];

        // Bleah, refactor this to order the results and Zip() them together!
        var result = measures.Select(m =>
        {
            var definition = definitions.First(d => d.Name == m.Name);
            var meta = metadata.First(md => md.Name == m.Name);
            var measure = new Measure(definition, meta.ToMeasureMetadata(), m.ToMeasureValue(definition));
            _measureCache.TryAdd(measure.Name, measure);
            return measure;
        });
        return result;
    }

    internal async Task<IEnumerable<Measure>> FindAllImplAsync(IEnumerable<string> names, CancellationToken cancellationToken, IDbTransaction? transaction)
    {
        var measures = await _connection.QueryAsync<MeasureDto>(SqlCommands.FindAllMeasuresByNames, new { Names = names }, transaction);
        var definitions = await _connection.QueryAsync<MeasureDefinition>(SqlCommands.FindAllDefinitionsByNames, new { Names = names }, transaction);
        var metadata = await _connection.QueryAsync<MeasureMetadataDto>(SqlCommands.FindAllMetadataByNames, new { Names = names }, transaction);

        if (cancellationToken.IsCancellationRequested)
            return [];

        // Bleah, refactor this to order the results and Zip() them together!
        return measures.Select(m =>
        {
            var definition = definitions.First(d => d.Name == m.Name);
            var meta = metadata.First(md => md.Name == m.Name);
            return new Measure(definition, meta.ToMeasureMetadata(), m.ToMeasureValue(definition));
        });
    }

    internal async Task<Measure?> FindImplAsync(string name, IDbTransaction? transaction)
    {
        if (_measureCache.TryGetValue(name, out var cachedMeasure))
            return cachedMeasure;

        var measureDto = await _connection.QueryFirstOrDefaultAsync<MeasureDto>(SqlCommands.FindMeasureByName, new { Name = name }, transaction: transaction);
        if (measureDto is null)
            return null;

        var definition = await _connection.QueryFirstAsync<MeasureDefinition>(SqlCommands.FindDefinitionByName, new { Name = name }, transaction: transaction);
        var metadata = await _connection.QueryFirstAsync<MeasureMetadataDto>(SqlCommands.FindMetadataByName, new { Name = name }, transaction: transaction);

        var measure = new Measure(definition, metadata.ToMeasureMetadata(), measureDto.ToMeasureValue(definition));
        _measureCache.TryAdd(name, measure);
        return measure;
    }

    internal Task<MeasureDefinition?> FindDefinitionImplAsync(string name, IDbTransaction? transaction)
        => _connection.QueryFirstOrDefaultAsync<MeasureDefinition>(SqlCommands.FindDefinitionByName, new { Name = name }, transaction);

    private readonly ConcurrentDictionary<string, Measure> _measureCache = new();
    private readonly HashSet<string> _sessionRegisteredMeasures = new();
    private readonly SqliteConnection _connection;
    private bool _disposed;

    private void InitializeDatabase()
    {
        _connection.Execute(SqlCommands.CreateDefinitionsTable);
        _connection.Execute(SqlCommands.CreateMetadataTable);
        _connection.Execute(SqlCommands.CreateMeasuresTable);
    }

    private void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        try
        {
            if (disposing)
            {
                _measureCache.Clear();
                _sessionRegisteredMeasures.Clear();
            }
        }
        finally
        {
            _disposed = true;
        }
    }
}
