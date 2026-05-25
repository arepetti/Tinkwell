using System.Text.Json;

namespace Tinkwell.Studio.Services;

public interface ITwCli
{
    Task<JsonElement> RunOneShotAsync(
        IReadOnlyList<string> args,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<JsonElement>> RunOneShotManyAsync(
        IReadOnlyList<string> args,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<JsonElement> StreamAsync(
        IReadOnlyList<string> args,
        CancellationToken cancellationToken = default);
}
