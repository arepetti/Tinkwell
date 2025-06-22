using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Tinkwell.Supervisor;

var host = Host.CreateDefaultBuilder(args)
   .AddWorker()
   .ConfigureLogging(logging =>
   {
       logging.ClearProviders();
       logging.AddConsole();
   })
   .Build();

await host.RunAsync();
