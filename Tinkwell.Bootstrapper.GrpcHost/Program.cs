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

await builder.DelegateConfigureServices();

var app = builder.Build();
var registry = app.Services.GetRequiredService<IRegistry>();

// We have only one discovery service (the master) when running multiple gRPC hosts.
// You can still view what's registered in each host with a GET of "/" for the exposed server
// (but it's intended for debugging, not to be consume by other apps/services).
if (string.IsNullOrWhiteSpace(app.Configuration.GetValue<string>("Discovery::Master")))
{
    app.MapGrpcService<DiscoveryService>();
    registry.AddGrpcEndpoint<DiscoveryService>(new() { Aliases = ["Discovery", "DiscoveryService"] });
}

await app.DelegateConfigureRoutes();

app.MapGet("/", registry.AsText);

app.Run();
