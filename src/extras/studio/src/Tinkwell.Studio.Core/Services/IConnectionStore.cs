namespace Tinkwell.Studio.Services;

/// <summary>
/// Persists the most recent successful coordinator connection across Studio
/// runs. The connection dialog uses the loaded value to pre-populate its fields
/// on the next launch.
/// </summary>
public interface IConnectionStore
{
    /// <summary>
    /// Returns the previously saved connection, or
    /// <see cref="CoordinatorConnection.LocalDefault"/> when nothing has been
    /// saved yet (or the persisted payload is unreadable).
    /// </summary>
    Task<CoordinatorConnection> LoadAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes the given connection to durable storage. Best effort: I/O errors
    /// are logged and swallowed because failing to persist must not block the
    /// app from continuing.
    /// </summary>
    Task SaveAsync(CoordinatorConnection connection, CancellationToken cancellationToken = default);
}
