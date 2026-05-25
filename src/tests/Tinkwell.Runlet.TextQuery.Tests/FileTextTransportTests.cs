using Tinkwell.Runlet.TextQuery.Transports;

namespace Tinkwell.Runlet.TextQuery.Tests;

public class FileTextTransportTests
{
    [Fact]
    public async Task QueryAsync_ReadsFile_AndTrimsTrailingWhitespace()
    {
        var path = Path.Combine(Path.GetTempPath(), $"tw-textquery-{Guid.NewGuid():N}.txt");
        await File.WriteAllTextAsync(path, "  hello world\r\n\r\n");
        try
        {
            await using var transport = new FileTextTransport(path);
            await transport.ConnectAsync(CancellationToken.None);

            var result = await transport.QueryAsync(command: null, lineTerminator: "\n", timeoutMs: 1000, CancellationToken.None);

            Assert.Equal("hello world", result);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [Fact]
    public async Task QueryAsync_EmptyFile_ReturnsEmptyString()
    {
        var path = Path.Combine(Path.GetTempPath(), $"tw-textquery-{Guid.NewGuid():N}.txt");
        await File.WriteAllTextAsync(path, "");
        try
        {
            await using var transport = new FileTextTransport(path);
            await transport.ConnectAsync(CancellationToken.None);

            var result = await transport.QueryAsync(null, "\n", 1000, CancellationToken.None);

            Assert.Equal("", result);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }
}
