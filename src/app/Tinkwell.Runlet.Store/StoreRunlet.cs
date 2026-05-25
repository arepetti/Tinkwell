using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Tinkwell.Runner;
using Tinkwell.Runlet.Store.Backend;
using Tinkwell.Runlet.Store.Grpc.V1;

namespace Tinkwell.Runlet.Store;

/// <summary>
/// gRPC runlet that exposes a key-value state store with optional TTL,
/// bucket visibility, and real-time change notifications.
/// </summary>
/// <remarks>
/// <para>Configuration settings (from the <c>.tw</c> file):</para>
/// <list type="bullet">
///   <item><c>storage</c> — <c>memory</c> (default) or <c>db</c> / <c>sqlite</c>
///     (SQLite).</item>
///   <item><c>path</c> — SQLite database file path (default:
///     <c>{DataPath}/store.db</c>).</item>
///   <item><c>expiration-interval-seconds</c> — TTL sweep interval
///     (default: 60).</item>
///   <item><c>load-initial-state</c> — <c>no</c> (default), <c>yes</c>, or
///     <c>always</c>. When <c>always</c> and <c>storage</c> is <c>memory</c>,
///     seeds the in-memory store from the DB file at <c>path</c> on startup.</item>
/// </list>
/// </remarks>
public sealed class StoreRunlet : IGrpcRunlet
{
    public void ConfigureServices(IServiceCollection services, IConfiguration settings)
    {
        var storage = settings["storage"] ?? "memory";
        var expirationSeconds = int.TryParse(
            settings["expiration-interval-seconds"], out var s) ? s : 60;
        var loadInitialState = settings["load-initial-state"] ?? "no";

        IStoreBackend backend = storage switch
        {
            "db" or "sqlite" => CreateSqliteBackend(settings),
            _ => CreateMemoryBackend(settings, loadInitialState)
        };

        services.AddSingleton(backend);
        services.AddSingleton<StoreNotifier>();
        services.AddHostedService(sp => new NotificationWorker(
            sp.GetRequiredService<StoreNotifier>(),
            sp.GetRequiredService<ILogger<NotificationWorker>>()));
        services.AddHostedService(sp => new ExpirationService(
            sp.GetRequiredService<IStoreBackend>(),
            sp.GetRequiredService<StoreNotifier>(),
            sp.GetRequiredService<ILogger<ExpirationService>>(),
            TimeSpan.FromSeconds(expirationSeconds)));
    }

    public void MapGrpcServices(IServiceCollection services)
    {
        services.AddSingleton<StateStoreService>();
    }

    public void MapGrpcEndpoints(IGrpcEndpointMapper mapper)
    {
        mapper.MapService<StateStoreService>(opts =>
        {
            opts.FriendlyName = "State Store";
            opts.FamilyName = "store";
        });
    }

    private static IStoreBackend CreateMemoryBackend(IConfiguration settings, string loadInitialState)
    {
        var backend = new MemoryStoreBackend();

        if (string.Equals(loadInitialState, "always", StringComparison.OrdinalIgnoreCase))
        {
            var dbPath = ResolveDbPath(settings);
            StoreSeeder.SeedAsync(backend, dbPath).GetAwaiter().GetResult();
        }

        return backend;
    }

    private static IStoreBackend CreateSqliteBackend(IConfiguration settings)
    {
        return new SqliteStoreBackend(ResolveDbPath(settings));
    }

    private static string ResolveDbPath(IConfiguration settings)
    {
        var path = settings["path"];
        return string.IsNullOrWhiteSpace(path)
            ? TinkwellEnvironment.GetFullDataPath("store.db")
            : TinkwellEnvironment.GetFullDataPath(path);
    }
}
