namespace Tinkwell.Package;

/// <summary>
/// Exception thrown when a package operation fails (invalid format,
/// security violation, integrity check failure, etc.).
/// </summary>
public sealed class PackageException : Exception
{
    /// <param name="message">The error message that explains the reason for the exception.</param>
    public PackageException(string message) : base(message) { }

    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that is the cause of this exception, if any.</param>
    public PackageException(string message, Exception? innerException) : base(message, innerException) { }
}
