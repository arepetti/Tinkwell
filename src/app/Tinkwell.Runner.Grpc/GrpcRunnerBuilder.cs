using System.Net;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Tinkwell.Logging;
using Tinkwell.Runner.Hosting;

namespace Tinkwell.Runner.Grpc;

/// <summary>
/// Builder for gRPC runner containers. Builds a <see cref="WebApplication"/>
/// with Kestrel configured for HTTP/2 on a coordinator-allocated endpoint.
/// </summary>
/// <remarks>
/// <para>
/// Runlets loaded into this runner must implement <see cref="IGrpcRunlet"/>.
/// Their gRPC services are registered via <see cref="IGrpcRunlet.MapGrpcServices"/>
/// during host building, and endpoints are mapped via
/// <see cref="IGrpcRunlet.MapGrpcEndpoints"/> after the host is built.
/// </para>
/// <para>
/// After endpoint mapping, the builder collects the
/// <see cref="ServiceDefinition"/> entries produced by the
/// <see cref="GrpcEndpointMapper"/> and registers them with the coordinator
/// so they are discoverable via <c>service find</c> / <c>service list</c>.
/// </para>
/// </remarks>
public sealed class GrpcRunnerBuilder : RunnerHostBuilder
{
    /// <summary>
    /// The IP the gRPC server will listen on.
    /// </summary>
    public IPAddress ListenAddress { get; private set; } = IPAddress.Loopback;

    /// <summary>
    /// The port the gRPC server will listen on, or <c>0</c> before the
    /// endpoint is allocated.
    /// </summary>
    public int ListenPort { get; private set; }

    private CoordinatorPipeClient? _client;
    private string? _runnerId;
    private TlsOptions _tlsOptions = new();

    private GrpcRunnerBuilder(string[] args) : base(args) { }

    /// <summary>
    /// Creates a new gRPC runner builder from the process arguments.
    /// </summary>
    public static GrpcRunnerBuilder Create(string[] args) => new(args);

    protected override async Task OnRunletsLoadedAsync(
        RunnerOptions options, CoordinatorPipeClient client, ILogger logger)
    {
        _client = client;
        _runnerId = options.RunnerId;

        var endpoint = await client.AllocateEndpointAsync(
            options.RunnerId, ListenAddress);

        ListenAddress = endpoint.Address;
        ListenPort = endpoint.Port;

        logger.LogDebug(
            "Coordinator allocated endpoint {Endpoint} for gRPC runner",
            endpoint);
    }

    protected override void ValidateRunlet(RunletState runlet)
    {
        if (runlet.Instance is not IGrpcRunlet)
            throw new InvalidOperationException(
                $"Runlet '{runlet.Descriptor.Name}' ({runlet.Instance.GetType().FullName}) " +
                $"does not implement IGrpcRunlet. The gRPC runner requires all runlets " +
                $"to implement IGrpcRunlet.");
    }

    protected override void ConfigureRunlet(
        IServiceCollection services, RunletState runlet, IConfiguration settings)
    {
        base.ConfigureRunlet(services, runlet, settings);
        ((IGrpcRunlet)runlet.Instance).MapGrpcServices(services);
    }

    protected override IHost BuildHost(
        string[] args, RunnerOptions options,
        CoordinatorPipeClient client, IReadOnlyList<RunletState> loaded,
        RunnerDescriptor descriptor)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Logging.AddTinkwellConsole();

        _tlsOptions = new TlsOptions();
        builder.Configuration.GetSection("Tls").Bind(_tlsOptions);

        builder.WebHost.ConfigureKestrel(kestrel =>
        {
            kestrel.Listen(ListenAddress, ListenPort, listenOptions =>
            {
                listenOptions.Protocols = HttpProtocols.Http2;

                if (_tlsOptions.IsEnabled)
                {
                    var cert = X509CertificateLoader.LoadPkcs12FromFile(
                        _tlsOptions.CertificatePath, password: _tlsOptions.CertificatePassword);
                    listenOptions.UseHttps(cert);
                }
            });
        });

        builder.Services.AddGrpc();
        builder.Services.AddSingleton(descriptor);
        builder.Services.AddSingleton(options);
        builder.Services.AddSingleton(client);
        AddRunnerTelemetry(builder.Services, builder.Configuration);
        AddServiceDiscovery(builder.Services, client, builder.Configuration);
        AddHealthServices(builder.Services, descriptor);

        foreach (var runlet in loaded)
        {
            var settings = new ConfigurationBuilder()
                .AddInMemoryCollection(runlet.Descriptor.Settings!)
                .Build();

            ConfigureRunlet(builder.Services, runlet, settings);
        }

        return builder.Build();
    }

    protected override async Task OnHostBuiltAsync(IHost host, IReadOnlyList<RunletState> runlets)
    {
        if (host is not WebApplication app)
            return;

        var mapper = new GrpcEndpointMapper(
            app, $"{ListenAddress}:{ListenPort}", _tlsOptions.Mode);

        foreach (var runlet in runlets)
        {
            if (runlet.Instance is IGrpcRunlet grpcRunlet)
                grpcRunlet.MapGrpcEndpoints(mapper);
        }

        if (_client is not null && _runnerId is not null && mapper.RegisteredServices.Count > 0)
            await _client.RegisterServicesAsync(_runnerId, mapper.RegisteredServices);

        var logger = app.Services.GetRequiredService<ILoggerFactory>()
            .CreateLogger<GrpcRunnerBuilder>();

        string protocol = _tlsOptions.IsEnabled ? "HTTPS/2" : "HTTP/2";
        logger.LogInformation(
            "gRPC runner listening on {Address}:{Port} ({Protocol}), {ServiceCount} service(s) registered",
            ListenAddress, ListenPort, protocol, mapper.RegisteredServices.Count);
    }
}
