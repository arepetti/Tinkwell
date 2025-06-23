using Tinkwell.Bootstrapper.Ensamble;
using Tinkwell.Bootstrapper.Expressions;
using Tinkwell.Bootstrapper.GrpcHost;
using Tinkwell.Bootstrapper.IO;
using Tinkwell.Bootstrapper.Ipc;
using Tinkwell.Bootstrapper.Rpc.ServerHost.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Services
    .AddSingleton<IRegistry, Registry>()
    .AddTransient<IFileSystem, PhysicalFileSytem>()
    .AddTransient<IEnsambleFileReader, EnsambleFileReader>()
    .AddTransient<IExpressionEvaluator, ExpressionEvaluator>()
    .AddTransient<IEnsambleConditionEvaluator, EnsambleConditionEvaluator>()
    .AddTransient<INamedPipeClient, NamedPipeClient>()
    .AddTransient<IActivity, RegisterServicesActivity>()
    .AddGrpc();

await builder.DelegateConfigureServicesAsync();

var app = builder.Build();
var registry = app.Services.GetRequiredService<IRegistry>();

// We have only one discovery service (the master) when running multiple gRPC hosts.
// You can still view what's registered in each host with a GET of "/" for the exposed server
// (but it's intended for debugging, not to be consume by other apps/services).
if (app.IsMasterGrpcServer())
{
    app.MapGrpcService<DiscoveryService>();
    registry.AddGrpcEndpoint<DiscoveryService>(new() { Aliases = ["Discovery", "DiscoveryService"] });
}

await app.DelegateConfigureRoutesAsync();

app.MapGet("/", registry.AsText);

using var watcher = ParentProcessWatcher.WhenNotFound(() =>
{
    app.Logger.LogWarning("Parent process has been terminated, quitting.");
    app.Lifetime.StopApplication();
});

app.Run();
