using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace Tinkwell.Studio.Services;

/// <summary>
/// JSON-backed <see cref="IConnectionStore"/>. The file lives under
/// <c>%LocalAppData%/Tinkwell/Studio/connection.json</c> by default; tests can
/// override the path via the secondary constructor.
/// </summary>
public sealed class ConnectionStore : IConnectionStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _filePath;
    private readonly ILogger<ConnectionStore> _logger;

    public ConnectionStore(ILogger<ConnectionStore> logger)
        : this(DefaultFilePath, logger)
    {
    }

    /// <summary>
    /// Test/extension hook: construct the store with a custom file path. The
    /// containing directory is created lazily on the first save.
    /// </summary>
    public ConnectionStore(string filePath, ILogger<ConnectionStore> logger)
    {
        _filePath = filePath;
        _logger = logger;
    }

    /// <summary>
    /// Default persistence path:
    /// <c>%LocalAppData%/Tinkwell/Studio/connection.json</c>. Public so the
    /// startup code can mention it in diagnostics if needed.
    /// </summary>
    public static string DefaultFilePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Tinkwell",
        "Studio",
        "connection.json");

    public async Task<CoordinatorConnection> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_filePath))
            return CoordinatorConnection.LocalDefault;

        try
        {
            await using var stream = File.OpenRead(_filePath);
            var payload = await JsonSerializer.DeserializeAsync<PersistedConnection>(
                stream, JsonOptions, cancellationToken).ConfigureAwait(false);

            return payload?.ToConnection() ?? CoordinatorConnection.LocalDefault;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (OutOfMemoryException) { Environment.FailFast("Out of memory"); throw; }
        catch (Exception ex)
        {
            // Corrupt or unreadable file: fall back to defaults so the user can
            // still launch Studio and pick a fresh connection.
            _logger.LogWarning(ex, "Could not read saved connection at {Path}", _filePath);
            return CoordinatorConnection.LocalDefault;
        }
    }

    public async Task SaveAsync(CoordinatorConnection connection, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);

        try
        {
            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            await using var stream = File.Create(_filePath);
            await JsonSerializer.SerializeAsync(
                stream,
                PersistedConnection.From(connection),
                JsonOptions,
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (OutOfMemoryException) { Environment.FailFast("Out of memory"); throw; }
        catch (Exception ex)
        {
            // Best-effort persistence: the app still works without the file, so
            // an I/O failure here is a warning rather than a fatal error.
            _logger.LogWarning(ex, "Could not save connection to {Path}", _filePath);
        }
    }

    /// <summary>
    /// On-disk shape. Kept separate from <see cref="CoordinatorConnection"/> so
    /// we can evolve the persisted format independently of the runtime record
    /// (e.g. by adding a schema version later).
    /// </summary>
    private sealed class PersistedConnection
    {
        public CoordinatorTransport Transport { get; set; }
        public string? PipeName { get; set; }
        public string? Machine { get; set; }
        public string? DockerContainer { get; set; }
        public bool UseDockerCompose { get; set; }

        public static PersistedConnection From(CoordinatorConnection connection) => new()
        {
            Transport = connection.Transport,
            PipeName = connection.PipeName,
            Machine = connection.Machine,
            DockerContainer = connection.DockerContainer,
            UseDockerCompose = connection.UseDockerCompose,
        };

        public CoordinatorConnection ToConnection() => new(
            Transport, PipeName, Machine, DockerContainer, UseDockerCompose);
    }
}
