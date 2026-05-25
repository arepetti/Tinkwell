namespace Tinkwell.Runlet.TextQuery;

/// <summary>
/// Abstraction over the different text transports (TCP, serial, file, command).
/// </summary>
internal interface ITextTransport : IAsyncDisposable
{
    Task ConnectAsync(CancellationToken ct);

    /// <summary>
    /// Sends a command and reads the response. For file/command transports,
    /// <paramref name="command"/> is ignored and the source is simply read.
    /// </summary>
    Task<string> QueryAsync(string? command, string lineTerminator, int timeoutMs, CancellationToken ct);
}
