namespace Tinkwell.Runlet.TextQuery.Transports;

internal sealed class FileTextTransport : ITextTransport
{
    private readonly string _path;

    public FileTextTransport(string path) => _path = path;

    public Task ConnectAsync(CancellationToken ct) => Task.CompletedTask;

    public async Task<string> QueryAsync(string? command, string lineTerminator, int timeoutMs, CancellationToken ct)
    {
        var content = await File.ReadAllTextAsync(_path, ct);
        return content.Trim();
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
