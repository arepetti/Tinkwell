namespace Tinkwell.Measures.Storage;

/// <summary>
/// Represents a transaction for a storage provider.
/// </summary>
public interface IStorageTransaction : IStorage, IDisposable
{
    /// <summary>
    /// Commits the transaction.
    /// </summary>
    ValueTask CommitAsync();

    /// <summary>
    /// Rolls back the transaction.
    /// </summary>
    ValueTask RollbackAsync();
}
