using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Tinkwell.Health;
using Tinkwell.Runner;

namespace Tinkwell.Runlet.ProtobufGateway;

/// <summary>
/// Headless runlet that starts a CoAP server and tunnels incoming protobuf
/// requests to discovered gRPC services. Access profiles are defined in
/// <c>protobuf-gateway</c> blocks in the <c>.tw</c> configuration file.
/// </summary>
/// <remarks>
/// <para>Runlet settings:</para>
/// <list type="bullet">
///   <item><c>port</c> — UDP port for the CoAP server (default: 5684).</item>
///   <item><c>name</c> — Runlet identity used to match <c>for</c> modifiers
///   on <c>protobuf-gateway</c> blocks. When omitted, only blocks with
///   <c>for "*"</c> (or no <c>for</c>) are matched.</item>
///   <item><c>path</c> — Config file path override (defaults to coordinator config).</item>
///   <item><c>max-concurrent-requests</c> — Maximum number of requests processed
///   concurrently by the CoAP server (default: 100).</item>
///   <item><c>max-pending-requests</c> — Maximum requests waiting for a concurrency
///   slot before new datagrams are rejected with 5.03 (default: 200, 0 = unlimited).</item>
/// </list>
/// </remarks>
public sealed class ProtobufGatewayRunlet : IRunlet
{
    public void ConfigureServices(IServiceCollection services, IConfiguration settings)
    {
        var port = settings.GetValue("port", 5684);
        var name = settings["name"];
        var configPath = settings["path"];
        var maxConcurrent = settings.GetValue("max-concurrent-requests", 100);
        var maxPending = settings.GetValue("max-pending-requests", 200);

        services.AddSingleton(new ProtobufGatewayOptions(port, name, configPath, maxConcurrent, maxPending));
        services.AddSingleton<ServiceCache>();

        var dropCheck = new IngestionDropCheck("protobuf-gateway-drops");
        services.AddSingleton(dropCheck);
        services.AddSingleton<IHealthCheck>(dropCheck);

        services.AddHostedService<ProtobufGatewayWorker>();
    }
}

internal sealed record ProtobufGatewayOptions(
    int Port,
    string? RunletName,
    string? ConfigPath,
    int MaxConcurrentRequests,
    int MaxPendingRequests);
