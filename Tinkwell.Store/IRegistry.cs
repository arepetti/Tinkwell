using UnitsNet;

namespace Tinkwell.Store;

public interface IRegistry
{
    event EventHandler<ValueChangedEventArgs<IQuantity>>? ValueChanged;

    void Register(QuantityMetadata metadata);
    void Update(string name, IQuantity value);
    QuantityMetadata Find(string name);
    IEnumerable<QuantityMetadata> FindAll();
    IQuantity? GetCurrentValue(string name);
    IEnumerable<IQuantity> GetHistory(string name);
}