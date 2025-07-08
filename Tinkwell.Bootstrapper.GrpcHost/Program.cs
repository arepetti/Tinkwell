using Tinkwell.Bootstrapper;
using Tinkwell.Bootstrapper.Ensamble;
using Tinkwell.Bootstrapper.Expressions;
using Tinkwell.Bootstrapper.GrpcHost;
using Tinkwell.Bootstrapper.IO;
using Tinkwell.Bootstrapper.Ipc;
using Tinkwell.Bootstrapper.Ipc.Extensions;
using Tinkwell.Bootstrapper.Rpc.ServerHost.Services;

var builder = WebApplication.CreateBuilder(args);

// We need to respect the order Port then Role.
// The supervisor has to have a full address for us before we can claim a role.
int port = builder.ClaimPort();
bool isMasterDiscovery = builder.TryClaimRole(
    WellKnownNames.DiscoveryServiceRoleName,
    out var masterAddress,
    out var localAddress);

builder.WebHost.ConfigureKestrel(options =>
{
    var certificate = builder.ResolveCertificate();
    options.ListenLocalhost(port, configure =>
    {
        configure.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http2;
        configure.UseHttps(certificate.Path, certificate.Password);
    });
    options.ListenAnyIP(port, configure =>
    {
        configure.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http2;
        configure.UseHttps(certificate.Path, certificate.Password);
    });
});

builder.Services
    .AddTransient<IExpressionEvaluator, ExpressionEvaluator>()
    .AddTransient<INamedPipeClient, NamedPipeClient>()
    .AddSingleton<IRegistry, Registry>()
    .AddTransient<IEnsambleConditionEvaluator, EnsambleConditionEvaluator>()
    .AddTransient<IActivity, RegisterServicesActivity>()
    .AddGrpc();

await builder.DelegateConfigureServicesAsync();

var app = builder.Build();
var registry = app.Services.GetRequiredService<IRegistry>();
registry.MasterAddress = masterAddress;
registry.LocalAddress = localAddress;

// We have only one discovery service (the master) when running multiple gRPC hosts.
// You can still view what's registered in each host with a GET of "/" for the exposed server
// (but it's intended for debugging, not to be consume by other apps/services).
if (isMasterDiscovery)
{
    app.MapGrpcService<DiscoveryService>();
    registry.AddGrpcEndpoint<DiscoveryService>(new() { Aliases = [Tinkwell.Services.Discovery.Descriptor.Name] });
}

await app.DelegateConfigureRoutesAsync();

app.MapGet("/", () => registry.AsText());

using var watcher = ParentProcessWatcher.WhenNotFound(() =>
{
    app.Logger.LogWarning("Parent process has been terminated, quitting.");
    app.Lifetime.StopApplication();
});

app.Lifetime.ApplicationStarted.Register(() =>
{
    var client = app.Services.GetRequiredService<INamedPipeClient>();
    client.SendCommandToSupervisorAndDisconnectAsync(app.Configuration, $"signal \"{HostingInformation.RunnerName}\"")
        .GetAwaiter()
        .GetResult();
});

app.Run();
