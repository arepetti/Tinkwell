namespace Tinkwell.Measures;

/// <summary>
/// Provides data for the <see cref="IRegistry.ValueChanged"/> event.
/// </summary>
/// <typeparam name="T">The type of the value that changed.</typeparam>
public class ValueChangedEventArgs<T> : EventArgs
{
    /// <summary>
    /// Gets the name of the measure that changed.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the old value of the measure.
    /// </summary>
    public T? OldValue { get; }

    /// <summary>
    /// Gets the new value of the measure.
    /// </summary>
    public T NewValue { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ValueChangedEventArgs{T}"/> class.
    /// </summary>
    /// <param name="name">The name of the measure that changed.</param>
    /// <param name="oldValue">The old value of the measure.</param>
    /// <param name="newValue">The new value of the measure.</param>
    public ValueChangedEventArgs(string name, T? oldValue, T newValue)
    {
        Name = name;
        OldValue = oldValue;
        NewValue = newValue;
    }
}