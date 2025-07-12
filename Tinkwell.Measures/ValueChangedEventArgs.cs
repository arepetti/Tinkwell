namespace Tinkwell.Measures;

/// <summary>
/// Provides data for the <see cref="IRegistry.ValueChanged"/> event.
/// </summary>
public class ValueChangedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the name of the measure that changed.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the old value of the measure.
    /// </summary>
    public MeasureValue? OldValue { get; }

    /// <summary>
    /// Gets the new value of the measure.
    /// </summary>
    public MeasureValue NewValue { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ValueChangedEventArgs{T}"/> class.
    /// </summary>
    /// <param name="name">The name of the measure that changed.</param>
    /// <param name="oldValue">The old value of the measure.</param>
    /// <param name="newValue">The new value of the measure.</param>
    public ValueChangedEventArgs(string name, MeasureValue? oldValue, MeasureValue newValue)
    {
        Name = name;
        OldValue = oldValue;
        NewValue = newValue;
    }
}