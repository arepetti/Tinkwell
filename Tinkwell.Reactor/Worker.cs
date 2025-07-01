using Microsoft.Extensions.Hosting;

namespace Tinkwell.Reactor;

sealed class Worker(Reactor reactor) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
        => _reactor.StartAsync(cancellationToken);

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _reactor.DisposeAsync();
    }

    private readonly Reactor _reactor = reactor;
}
