namespace Tinkwell.Runlet.EventPersistence;

/// <summary>
/// Resolved settings for <see cref="EventPersistenceRunlet"/>, built in
/// <see cref="EventPersistenceRunlet.ConfigureServices"/> after parsing and
/// clamping runlet configuration values.
/// </summary>
/// <param name="DbPath">SQLite database file path (including the value of <c>db-path</c>).</param>
/// <param name="BatchSize">Maximum number of events per SQLite write transaction; already clamped to 1–10,000.</param>
/// <param name="FlushInterval">Maximum time to retain a non-full batch before writing; already clamped to 0.001–3600 seconds.</param>
internal sealed record EventPersistenceOptions(
    string DbPath,
    int BatchSize,
    TimeSpan FlushInterval);
