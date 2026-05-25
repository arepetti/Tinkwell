using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Tinkwell.Expressions;
using Tinkwell.Expressions.Functions;
using Tinkwell.Health;
using Tinkwell.Measures;
using Tinkwell.Measures.Functions;
using Tinkwell.Runner;
using Tinkwell.Runlet.Measures.Registry;
using Tinkwell.Runner.Hosting;
using Tinkwell.Runlet.Measures.Grpc.V1;

namespace Tinkwell.Runlet.Measures;

/// <summary>
/// gRPC runlet that hosts a dedicated Measures gRPC service backed by an
/// in-process <see cref="Tinkwell.Measures.IMeasureRegistry"/>. Optionally
/// runs a background worker that reads measure definitions from a
/// <c>.tw</c> configuration file and registers them automatically.
/// </summary>
/// <remarks>
/// <para>Configuration settings (from the <c>.tw</c> file):</para>
/// <list type="bullet">
///   <item><c>path</c> — Path to the measures <c>.tw</c> file. Defaults to
///     the coordinator's own configuration file.</item>
///   <item><c>bucket</c> — State store bucket for measure data
///     (default: <c>measures</c>).</item>
///   <item><c>calculated-measures</c> — When <c>true</c> (default), registers
///     <see cref="DerivedMeasureWorker"/> to load the file and register measures.
///     Other runlets may depend on <see cref="MeasuresConfigReady"/>; keep
///     <c>true</c> unless another mechanism populates the registry and signals
///     readiness.</item>
///   <item><c>derived-channel-capacity</c> — Bounded channel size for
///     <c>DerivedMeasureWorker</c> (default: <c>256</c>).</item>
///   <item><c>derived-channel-full-mode</c> — <see cref="T:System.Threading.Channels.BoundedChannelFullMode"/>
///     when the channel is full (default: <c>DropWrite</c>).</item>
/// </list>
/// </remarks>
public sealed class MeasuresRunlet : IGrpcRunlet
{
    private string? _configPath;
    private string _bucketId = "measures";
    private bool _calculatedMeasures = true;

    public void ConfigureServices(IServiceCollection services, IConfiguration settings)
    {
        _configPath = settings["path"];
        _bucketId = settings["bucket"] ?? "measures";
        _calculatedMeasures = !string.Equals(
            settings["calculated-measures"], "false", StringComparison.OrdinalIgnoreCase);

        var derivedCapacity = int.TryParse(settings["derived-channel-capacity"], out var dc) ? dc : 256;
        var derivedFullMode = ParseFullMode(settings["derived-channel-full-mode"],
            System.Threading.Channels.BoundedChannelFullMode.DropWrite);

        var holder = new MeasureRegistryHolder();
        var configReady = new MeasuresConfigReady();
        services.AddSingleton(holder);
        services.AddSingleton(configReady);
        services.AddSingleton(new MeasuresRunletOptions(
            _configPath, _bucketId,
            new ChannelConfig(derivedCapacity, derivedFullMode)));
        var functions = ExpressionFunctionDiscovery.BuiltIn()
            .Concat(ExpressionFunctionDiscovery.FromAssemblyOf<QuantityFunction>())
            .ToList();
        services.AddSingleton<IExpressionEvaluator>(new ExpressionEvaluator(functions));
        services.AddHostedService<MeasureWatchWorker>();

        if (_calculatedMeasures)
        {
            var derivedBackpressure = new ChannelBackpressureCheck(
                "derived-measures", derivedCapacity);
            services.AddSingleton(derivedBackpressure);
            services.AddSingleton<IHealthCheck>(derivedBackpressure);

            services.AddHostedService<DerivedMeasureWorker>();
        }
    }

    public void MapGrpcServices(IServiceCollection services)
    {
        services.AddSingleton<MeasuresGrpcService>();
    }

    public void MapGrpcEndpoints(IGrpcEndpointMapper mapper)
    {
        mapper.MapService<MeasuresGrpcService>(opts =>
        {
            opts.FriendlyName = "Measures";
            opts.FamilyName = "measures";
        });
    }

    public async Task StartAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var holder = services.GetRequiredService<MeasureRegistryHolder>();
        var discovery = services.GetRequiredService<IServiceDiscovery>();
        var logger = services.GetRequiredService<ILoggerFactory>()
            .CreateLogger<MeasuresRunlet>();

        logger.LogDebug("Creating MeasureRegistry (bucket: {Bucket})", _bucketId);

        var registry = await MeasureRegistryFactory.CreateAsync(
            discovery,
            _bucketId,
            services.GetRequiredService<ILogger<MeasureRegistry>>(),
            cancellationToken);

        holder.Set(registry);

        if (!_calculatedMeasures)
        {
            var configReady = services.GetRequiredService<MeasuresConfigReady>();
            configReady.Set(new Tinkwell.Runlet.Measures.Configuration.MeasuresConfig(
                Array.Empty<Tinkwell.Runlet.Measures.Configuration.MeasureConfigEntry>()));
            logger.LogDebug(
                "calculated-measures is disabled — signalled empty MeasuresConfigReady");
        }

        logger.LogDebug("MeasureRegistry initialized");
    }

    private static System.Threading.Channels.BoundedChannelFullMode ParseFullMode(
        string? value, System.Threading.Channels.BoundedChannelFullMode defaultMode)
    {
        if (value is null)
            return defaultMode;
        if (System.Enum.TryParse<System.Threading.Channels.BoundedChannelFullMode>(value, true, out var mode))
            return mode;
        return defaultMode;
    }
}

/// <summary>
/// Settings resolved from the runlet's <c>.tw</c> configuration block.
/// </summary>
internal sealed record MeasuresRunletOptions(
    string? ConfigPath,
    string BucketId,
    ChannelConfig DerivedChannelConfig);
