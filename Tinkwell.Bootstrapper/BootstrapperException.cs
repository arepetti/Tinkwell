namespace Tinkwell.Bootstrapper;

/// <summary>
/// Represents errors that occur during Tinkwell bootstrapper operations.
/// </summary>
[Serializable]
public sealed class BootstrapperException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BootstrapperException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public BootstrapperException(string message) : base(message) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="BootstrapperException"/> class with a specified error message and a reference to the inner exception that is the cause of this error.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public BootstrapperException(string message, Exception innerException) : base(message, innerException) { }
}

