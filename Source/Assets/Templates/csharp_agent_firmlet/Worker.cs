using Microsoft.Extensions.Hosting;

namespace {{namespace}};

// Use this class if you need to perform expensive initializations you
// want to keep outside the implementation or if you need to initialize
// multiple services. You could skip this class but it's just a convention in Tinkwell.

sealed class Worker({{name}} implementation) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
        => _implementation.StartAsync(cancellationToken);

    public async Task StopAsync(CancellationToken cancellationToken)
        => await _implementation.StopAsync(cancellationToken);

    private readonly {{name}} _implementation = implementation;
}
