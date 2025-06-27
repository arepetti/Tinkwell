using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Tinkwell.Bootstrapper.DllHost;
using Tinkwell.Bootstrapper.Ipc;

var builder = Host.CreateDefaultBuilder(args)
   .AddWorker()
   .ConfigureLogging(logging =>
   {
       logging.ClearProviders();
       logging.AddConsole();
   });

await builder.DelegateConfigureServicesAsync(args);

var host = builder.Build();

using var watcher = ParentProcessWatcher.WhenNotFound(() =>
{
    var logger = host.Services.GetRequiredService<ILogger<Program>>();
    logger.LogWarning("Parent process has been terminated, quitting.");
    host.StopAsync();
});

await host.RunAsync();
