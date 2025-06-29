using Microsoft.Extensions.Hosting;

namespace Tinkwell.Reducer;

sealed class Worker(Reducer reducer) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
        => _reducer.StartAsync(cancellationToken);

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _reducer.DisposeAsync();
    }

    private readonly Reducer _reducer = reducer;
}
