namespace Tinkwell;

/// <summary>
/// Base exception for all Tinkwell domain errors. Derive from this
/// class instead of <see cref="Exception"/> so that callers can catch
/// the entire Tinkwell error hierarchy with a single type.
/// </summary>
public abstract class TinkwellException : Exception
{
    /// <summary>
    /// Initializes a new instance with the specified message.
    /// </summary>
    protected TinkwellException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance with the specified message and inner exception.
    /// </summary>
    protected TinkwellException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
