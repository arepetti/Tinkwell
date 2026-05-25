using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Tinkwell.Coordinator;

/// <summary>
/// Configuration for the coordinator's endpoint allocation.
/// </summary>
public sealed class EndpointOptions
{
    /// <summary>
    /// First port to probe when allocating an endpoint.
    /// </summary>
    public int BasePort { get; set; } = 4900;

    /// <summary>
    /// Number of ports to try starting from <see cref="BasePort"/>.
    /// </summary>
    public int PortRange { get; set; } = 100;
}

/// <summary>
/// Thread-safe port allocator. Assignments are keyed by runner <em>name</em>
/// (not the short hex ID, which changes on restart) so that a restarted
/// runner gets back the same port it had before the crash.
/// </summary>
internal sealed class EndpointAllocator
{
    private readonly Lock _lock = new();
    private readonly Dictionary<string, IPEndPoint> _assignments = new(StringComparer.Ordinal);
    private readonly EndpointOptions _options;
    private readonly ILogger<EndpointAllocator> _logger;

    public EndpointAllocator(IOptions<EndpointOptions> options, ILogger<EndpointAllocator> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Returns a previously assigned endpoint for the runner, or probes
    /// for the first available port on <paramref name="address"/> and
    /// records the assignment.
    /// </summary>
    public IPEndPoint Allocate(string runnerName, IPAddress address)
    {
        lock (_lock)
        {
            if (_assignments.TryGetValue(runnerName, out var existing)
                && existing.Address.Equals(address))
            {
                _logger.LogTrace(
                    "Returning cached endpoint {Endpoint} for runner '{Name}'",
                    existing, runnerName);
                return existing;
            }

            var usedPorts = new HashSet<int>(
                _assignments.Values
                    .Where(ep => ep.Address.Equals(address))
                    .Select(ep => ep.Port));

            int basePort = _options.BasePort;
            int range = _options.PortRange;

            for (int port=basePort; port < basePort + range; ++port)
            {
                if (usedPorts.Contains(port))
                    continue;

                if (!IsPortAvailable(address, port))
                    continue;

                var endpoint = new IPEndPoint(address, port);
                _assignments[runnerName] = endpoint;

                _logger.LogInformation(
                    "Allocated endpoint {Endpoint} for runner '{Name}'",
                    endpoint, runnerName);

                return endpoint;
            }

            throw new IOException(
                $"No available port for {address} in range {basePort}–{basePort + range - 1}");
        }
    }

    private static bool IsPortAvailable(IPAddress address, int port)
    {
        try
        {
            using var listener = new TcpListener(address, port);
            listener.Start();
            listener.Stop();
            return true;
        }
        catch (SocketException)
        {
            return false;
        }
    }
}
