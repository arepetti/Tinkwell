using Microsoft.Extensions.Hosting;

namespace Tinkwell.Actions.Executor;

sealed class Worker(Executor executor) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
        => _executor.StartAsync(cancellationToken);

    public async Task StopAsync(CancellationToken cancellationToken)
        => await _executor.DisposeAsync();

    private readonly Executor _executor = executor;
}
