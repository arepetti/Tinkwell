using Microsoft.Extensions.Hosting;

namespace Tinkwell.Bootstrapper.DllHost;

interface IActivity
{
    Task ConfigureBuilderAsync(IHostBuilder builder, CancellationToken cancellationToken);
}
