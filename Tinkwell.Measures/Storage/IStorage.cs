namespace Tinkwell.Measures.Storage;

/// <summary>
/// Represents a storage provider for measures.
/// </summary>
public interface IStorage
{
    /// <summary>
    /// Occurs when a measure value changes.
    /// </summary>
    /// <remarks>
    /// Note that the storage implementation has to ensure that this event is raised
    /// only when the value has been changes using its own instance, external changes
    /// go unnoticed (as far as <c>IStorage</c> is concerned).
    /// </remarks>
    event EventHandler<ValueChangedEventArgs<MeasureValue>>? ValueChanged;

    /// <summary>
    /// Gets a value indicating whether the storage provider supports transactions.
    /// </summary>
    bool SupportsTransactions { get; }

    /// <summary>
    /// Begins a new transaction.
    /// </summary>
    /// <remarks>
    /// To perform operations within a transaction, you must call this method and
    /// do any update using the returned <c>IStorage</c> instance. At the end you must
    /// call <c>IStorageTransaction.Commit()</c> to commit all your changes at once or
    /// <c>IStorageTransaction.Rollback()</c> to rollback any changes you made.
    /// </remarks>
    /// <returns>A new transaction.</returns>
    /// <exception cref="NotSupportedException">
    /// If <c>SupportsTransactions</c> is <c>false</c>.
    /// </exception>
    IStorageTransaction BeginTransaction();

    /// <summary>
    /// Registers a new measure.
    /// </summary>
    /// <param name="definition">The definition of the measure.</param>
    /// <param name="metadata">The metadata of the measure.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>true if the measure was registered; otherwise, false.</returns>
    ValueTask<bool> RegisterAsync(MeasureDefinition definition, MeasureMetadata metadata, CancellationToken cancellationToken);

    /// <summary>
    /// Deregisters a measure.
    /// </summary>
    /// <param name="name">The name of the measure to deregister.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    ValueTask DeregisterAsync(string name, CancellationToken cancellationToken);

    /// <summary>
    /// Updates the value of a measure.
    /// </summary>
    /// <param name="name">The name of the measure to update.</param>
    /// <param name="value">The new value.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The updated measure.</returns>
    ValueTask<Measure> UpdateAsync(string name, MeasureValue value, CancellationToken cancellationToken);

    /// <summary>
    /// Finds all measures.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A collection of all measures.</returns>
    /// <remarks>
    /// If the storage provider supports transactions and you're invoking this from a transaction
    /// scope then it should include the changes you made (but see later).
    /// All changes performed while querying, might be included or not, there is no guarantee.
    /// </remarks>
    ValueTask<IEnumerable<Measure>> FindAllAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Finds all measures with the specified names.
    /// </summary>
    /// <param name="names">The names of the measures to find.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A collection of measures with the specified names.</returns>
    /// <remarks>
    /// If the storage provider supports transactions and you're invoking this from a transaction
    /// scope then it should include the changes you made (but see later).
    /// All changes performed while querying, might be included or not, there is no guarantee.
    /// </remarks>
    ValueTask<IEnumerable<Measure>> FindAllAsync(IEnumerable<string> names, CancellationToken cancellationToken);

    /// <summary>
    /// Tries to find a measure by name.
    /// </summary>
    /// <param name="name">The name of the measure to find.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <param name="measure">The measure with the specified name.</param>
    /// <returns>true if the measure was found; otherwise, false.</returns>
    /// <remarks>
    /// If the storage provider supports transactions and you're invoking this from a transaction
    /// scope then it should include the changes you made (but see later).
    /// </remarks>
    ValueTask<bool> TryFindAsync(string name, CancellationToken cancellationToken, out Measure measure);

    /// <summary>
    /// Finds a measure by name.
    /// </summary>
    /// <param name="name">The name of the measure to find.</param>
    /// <returns>The measure with the specified name.</returns>
    /// <remarks>
    /// Classes implementing this method should try as hard as possible to make sure
    /// this is a truly synchronous operation.
    /// If the storage provider supports transactions and you're invoking this from a transaction
    /// scope then it should include the changes you made (but see later).
    /// </remarks>
    Measure? Find(string name);

    /// <summary>
    /// Finds all measure definitions.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A collection of all measure definitions.</returns>
    /// <remarks>
    /// If the storage provider supports transactions and you're invoking this from a transaction
    /// scope then it should include the changes you made (but see later).
    /// All changes performed while querying, might be included or not, there is no guarantee.
    /// </remarks>
    ValueTask<IEnumerable<MeasureDefinition>> FindAllDefinitionsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Finds all measure definitions with the specified names.
    /// </summary>
    /// <param name="names">The names of the measures to find.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A collection of measure definitions with the specified names.</returns>
    /// <remarks>
    /// If the storage provider supports transactions and you're invoking this from a transaction
    /// scope then it should include the changes you made (but see later).
    /// All changes performed while querying, might be included or not, there is no guarantee.
    /// </remarks>
    ValueTask<IEnumerable<MeasureDefinition>> FindAllDefinitionsAsync(IEnumerable<string> names, CancellationToken cancellationToken);
}
