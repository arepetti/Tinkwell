namespace Tinkwell.Studio.Services;

/// <summary>
/// Runs a single <c>tw ping</c> against a candidate <see cref="CoordinatorConnection"/>.
/// Used by the connection dialog to validate the user's choice before committing
/// the values to <see cref="StudioSettings"/>.
/// </summary>
public interface ICoordinatorProbe
{
    /// <summary>
    /// Pings the coordinator described by <paramref name="connection"/>. Returns
    /// <see cref="ProbeResult.Ok"/> when the process exits with code 0 within the
    /// 5-second probe budget; otherwise <see cref="ProbeResult.Failed"/> with a
    /// human-readable error.
    /// </summary>
    Task<ProbeResult> PingAsync(CoordinatorConnection connection, CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of a single probe attempt. <see cref="Error"/> is populated only when
/// <see cref="Success"/> is <c>false</c> and is suitable for direct display in
/// the connection dialog.
/// </summary>
public sealed record ProbeResult(bool Success, string? Error)
{
    public static ProbeResult Ok { get; } = new(true, null);

    public static ProbeResult Failed(string error) => new(false, error);
}
