namespace Tinkwell.Store;

public class ValueChangedEventArgs<T> : EventArgs
{
    public string Name { get; }

    public T? OldValue { get; }

    public T NewValue { get; }

    public ValueChangedEventArgs(string name, T? oldValue, T newValue)
    {
        Name = name;
        OldValue = oldValue;
        NewValue = newValue;
    }
}