namespace Tinkwell.Pipes;

/// <summary>
/// Configuration for a <see cref="PipeServer"/>.
/// </summary>
public sealed class PipeServerOptions
{
    /// <summary>
    /// The base pipe name. Defaults to <c>tinkwell-coordinator</c>.
    /// </summary>
    public string PipeName { get; set; } = "tinkwell-coordinator";

    /// <summary>
    /// When <see langword="true"/> and the base <see cref="PipeName"/> is already
    /// in use, the server appends <c>-1</c>, <c>-2</c>, ... up to
    /// <see cref="MaxFallbackAttempts"/> until a free name is found.
    /// When <see langword="false"/>, the server throws if the name is in use.
    /// </summary>
    public bool AllowPipeNameFallback { get; set; } = true;

    /// <summary>
    /// Maximum number of alternate names to try when <see cref="AllowPipeNameFallback"/>
    /// is enabled.
    /// </summary>
    public int MaxFallbackAttempts { get; set; } = 10;

    /// <summary>
    /// Time in milliseconds to wait for a client to send data after connecting
    /// before the connection is dropped. Use <see cref="Timeout.Infinite"/> to
    /// wait indefinitely.
    /// </summary>
    public int ConnectionTimeoutMs { get; set; } = 30_000;
}
