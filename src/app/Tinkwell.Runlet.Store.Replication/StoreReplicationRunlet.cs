using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Tinkwell.Runner;
using Tinkwell.Runlet.Store.Replication.Grpc.V1;

namespace Tinkwell.Runlet.Store.Replication;

/// <summary>
/// Experimental companion runlet for master-slave store replication.
/// Must be listed after the <c>store</c> runlet in the same gRPC runner
/// so that <c>IStoreBackend</c> and <c>StoreNotifier</c> are available.
/// </summary>
public sealed class StoreReplicationRunlet : IGrpcRunlet
{
    public void ConfigureServices(IServiceCollection services, IConfiguration settings)
    {
        var role = settings["role"] switch
        {
            "master" => StoreRole.Master,
            "slave" => StoreRole.Slave,
            _ => throw new InvalidOperationException(
                "store-replication requires role = \"master\" or \"slave\"")
        };

        var options = new ReplicationOptions
        {
            Role = role,
            MasterAddress = settings["master-address"],
            ReconnectSeconds = int.TryParse(settings["reconnect-seconds"], out var r) ? r : 5
        };

        if (role == StoreRole.Slave && string.IsNullOrWhiteSpace(options.MasterAddress))
        {
            throw new InvalidOperationException(
                "store-replication slave requires master-address");
        }

        var mode = new StoreMode { Role = role };
        if (role == StoreRole.Master)
        {
            mode.SetReady();
        }

        services.AddSingleton(options);
        services.AddSingleton(mode);

        if (role == StoreRole.Slave)
        {
            services.AddHostedService(sp => new ReplicationWorker(
                sp.GetRequiredService<Backend.IStoreBackend>(),
                sp.GetRequiredService<StoreNotifier>(),
                sp.GetRequiredService<StoreMode>(),
                sp.GetRequiredService<ReplicationOptions>(),
                sp.GetRequiredService<ILogger<ReplicationWorker>>()));
        }
    }

    public void MapGrpcServices(IServiceCollection services)
    {
        services.AddSingleton<StoreReplicationService>();
    }

    public void MapGrpcEndpoints(IGrpcEndpointMapper mapper)
    {
        mapper.MapService<StoreReplicationService>(opts =>
        {
            opts.FriendlyName = "Store Replication";
            opts.FamilyName = "store-replication";
        });
    }
}
