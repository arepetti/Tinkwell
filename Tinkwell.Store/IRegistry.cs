using UnitsNet;

namespace Tinkwell.Store;

public interface IRegistry
{
    event EventHandler<ValueChangedEventArgs<IQuantity>>? ValueChanged;

    void Register(MeasureMetadata metadata);
    void Update(string name, IQuantity value);
    MeasureMetadata Find(string name);
    IEnumerable<MeasureMetadata> FindAll();
    IQuantity? GetCurrentValue(string name);
    IEnumerable<IQuantity> GetHistory(string name);
}