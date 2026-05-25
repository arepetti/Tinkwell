using Microsoft.Extensions.Logging.Abstractions;
using Tinkwell;
using Tinkwell.Runner.Hosting;

namespace Tinkwell.Runner.Hosting.Tests;

public class ServiceDiscoveryTests
{
    [Fact]
    public void Dispose_CanBeCalled_WhenNoChannelsAllocated()
    {
        var client = new CoordinatorPipeClient("unused-pipe", NullLogger<CoordinatorPipeClient>.Instance);
        var tls = new TlsOptions { Mode = TlsMode.None };
        var discovery = new ServiceDiscovery(client, tls);
        discovery.Dispose();
    }
}
