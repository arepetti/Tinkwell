using Microsoft.Extensions.Logging;
using Tinkwell.Measures;
using Tinkwell.Runner;
using Tinkwell.Runlet.Store.Grpc.V1;

namespace Tinkwell.Runlet.Measures.Registry;

/// <summary>
/// Creates a <see cref="MeasureRegistry"/> by discovering the state store
/// service through the coordinator.
/// </summary>
internal static class MeasureRegistryFactory
{
    public static async Task<IMeasureRegistry> CreateAsync(
        IServiceDiscovery discovery,
        string bucketId,
        ILogger<MeasureRegistry> logger,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(discovery);
        ArgumentException.ThrowIfNullOrWhiteSpace(bucketId);
        ArgumentNullException.ThrowIfNull(logger);

        var client = await discovery.CreateInstanceAsync<StateStore.StateStoreClient>("store", ct);

        return new MeasureRegistry(client, bucketId, logger);
    }
}
