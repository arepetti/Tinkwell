using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Tinkwell.Measures.History;
using Tinkwell.Runlet.MeasureHistory.Grpc.V1;
using Tinkwell.Runner;

namespace Tinkwell.Runlet.MeasureHistory;

/// <summary>
/// gRPC runlet that subscribes to the measures <see cref="Tinkwell.Runlet.Measures.Grpc.V1.Measures.Watch"/>
/// stream, persists values via <see cref="IMeasureHistoryStore"/>, and exposes query RPCs.
/// </summary>
public sealed class MeasureHistoryRunlet : IGrpcRunlet
{
    public void ConfigureServices(IServiceCollection services, IConfiguration settings)
    {
        var backend = settings["backend"]?.Trim()
            ?? throw new InvalidOperationException("measure-history requires backend.");

        var connectionString = settings["connection-string"];
        var batchSize = int.TryParse(settings["batch-size"], out var bs) ? bs : 100;
        var flushIntervalMs = int.TryParse(settings["flush-interval-ms"], out var fi) ? fi : 500;

        if (batchSize < 1)
            throw new InvalidOperationException("measure-history batch-size must be at least 1.");
        if (flushIntervalMs < 1)
            throw new InvalidOperationException("measure-history flush-interval-ms must be at least 1.");

        var options = new MeasureHistoryOptions
        {
            Backend = backend,
            ConnectionString = connectionString,
            BatchSize = batchSize,
            FlushIntervalMs = flushIntervalMs,
        };

        services.AddSingleton(options);
        services.AddSingleton<MeasureHistoryStoreHolder>();
        services.AddHostedService<MeasureHistoryWorker>();
    }

    public void MapGrpcServices(IServiceCollection services)
    {
        services.AddSingleton<MeasureHistoryGrpcService>();
    }

    public void MapGrpcEndpoints(IGrpcEndpointMapper mapper)
    {
        mapper.MapService<MeasureHistoryGrpcService>(opts =>
        {
            opts.FriendlyName = "Measure History";
            opts.FamilyName = "measure-history";
        });
    }

    public Task StartAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var options = services.GetRequiredService<MeasureHistoryOptions>();
        var holder = services.GetRequiredService<MeasureHistoryStoreHolder>();
        var logger = services.GetRequiredService<ILoggerFactory>()
            .CreateLogger<MeasureHistoryRunlet>();

        var store = CreateHistoryStore(options);
        holder.Set(store);

        logger.LogInformation(
            "Measure history store initialized (backend: {Backend})",
            options.Backend);

        return Task.CompletedTask;
    }

    public async Task StopAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var holder = services.GetRequiredService<MeasureHistoryStoreHolder>();
        if (holder.Store is null)
            return;

        if (holder.Store is IAsyncDisposable asyncDisposable)
            await asyncDisposable.DisposeAsync();
        else if (holder.Store is IDisposable disposable)
            disposable.Dispose();
    }

    private static IMeasureHistoryStore CreateHistoryStore(MeasureHistoryOptions options)
    {
        var assemblyName = options.Backend;
        Assembly assembly;
        try
        {
            assembly = Assembly.Load(assemblyName);
        }
        catch (OutOfMemoryException) { Environment.FailFast("Out of memory"); throw; }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to load history backend assembly '{assemblyName}'. " +
                $"Ensure the assembly is deployed next to the runner.", ex);
        }

        var storeType = assembly.GetTypes()
            .Where(static t => t is { IsClass: true, IsAbstract: false })
            .FirstOrDefault(t => typeof(IMeasureHistoryStore).IsAssignableFrom(t));

        if (storeType is null)
        {
            throw new InvalidOperationException(
                $"Assembly '{assembly.FullName}' does not contain a concrete type implementing " +
                $"{nameof(IMeasureHistoryStore)}.");
        }

        try
        {
            var instance = Activator.CreateInstance(storeType, options.ConnectionString);
            if (instance is not IMeasureHistoryStore store)
            {
                throw new InvalidOperationException(
                    $"Type '{storeType.FullName}' could not be constructed as {nameof(IMeasureHistoryStore)}.");
            }
            return store;
        }
        catch (MissingMethodException ex)
        {
            throw new InvalidOperationException(
                $"Type '{storeType.FullName}' must expose a public constructor accepting the connection string " +
                $"(single parameter of type string or string?).",
                ex);
        }
    }
}