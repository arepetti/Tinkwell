namespace Tinkwell.Store.Storage;

public interface IStorageStrategy
{
    event EventHandler<ValueChangedEventArgs<MeasureValue>>? ValueChanged;

    ValueTask<bool> RegisterAsync(MeasureMetadata metadata, CancellationToken cancellationToken);

    ValueTask UpdateAsync(string name, MeasureValue value, CancellationToken cancellationToken);

    ValueTask<IEnumerable<Measure>> FindAllAsync(CancellationToken cancellationToken);

    ValueTask<IEnumerable<Measure>> FindAllAsync(IEnumerable<string> names, CancellationToken cancellationToken);

    ValueTask<bool> TryFindAsync(string name, CancellationToken cancellationToken, out Measure measure);

    Measure? Find(string name);
}
