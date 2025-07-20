using System.Data;
using Tinkwell.Measures;
using Tinkwell.Measures.Storage;

namespace Tinkwell.Store.Storage.Sqlite;

sealed class SqliteStorageTransaction : IStorageTransaction
{
    internal SqliteStorageTransaction(SqliteStorage storage, IDbTransaction transaction)
    {
        _storage = storage;
        _transaction = transaction;
    }

    // To consider: is it correct to attach a listener to an object that represents a transaction?
    public event EventHandler<ValueChangedEventArgs>? ValueChanged
    {
        add => _storage.ValueChanged += value;
        remove => _storage.ValueChanged -= value;
    }

    public bool SupportsTransactions => false;

    public IStorageTransaction BeginTransaction()
        => throw new InvalidOperationException("Cannot begin a new transaction from within a transaction.");

    public async ValueTask<bool> RegisterAsync(MeasureDefinition definition, MeasureMetadata metadata, CancellationToken cancellationToken)
         => await _storage.RegisterImplAsync(definition, metadata, _transaction);

    public async ValueTask DeregisterAsync(string name, CancellationToken cancellationToken)
        => await _storage.DeregisterImplAsync(name, _transaction);

    public async ValueTask<Measure> UpdateAsync(string name, MeasureValue value, CancellationToken cancellationToken)
        => await _storage.UpdateImplAsync(name, value, cancellationToken, _transaction);

    public async ValueTask<IEnumerable<Measure>> FindAllAsync(CancellationToken cancellationToken)
        => await _storage.FindAllImplAsync(cancellationToken, _transaction);

    public async ValueTask<IEnumerable<Measure>> FindAllAsync(IEnumerable<string> names, CancellationToken cancellationToken)
        => await _storage.FindAllImplAsync(names, cancellationToken, _transaction);

    public Measure? Find(string name)
        => _storage.FindImplAsync(name, _transaction).GetAwaiter().GetResult();

    public MeasureDefinition? FindDefinition(string name)
        => _storage.FindDefinitionImplAsync(name, _transaction).GetAwaiter().GetResult();

    public ValueTask<IEnumerable<MeasureDefinition>> FindAllDefinitionsAsync(CancellationToken cancellationToken)
        => _storage.FindAllDefinitionsAsync(cancellationToken);

    public ValueTask<IEnumerable<MeasureDefinition>> FindAllDefinitionsAsync(IEnumerable<string> names, CancellationToken cancellationToken)
        => _storage.FindAllDefinitionsAsync(names, cancellationToken);

    public ValueTask CommitAsync()
    {
        _transaction.Commit();
        return ValueTask.CompletedTask;
    }

    public ValueTask RollbackAsync()
    {
        _transaction.Rollback();
        return ValueTask.CompletedTask;
    }

    public void Dispose()
        => _transaction.Dispose();


    private readonly IDbTransaction _transaction;
    private readonly SqliteStorage _storage;
}
