using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Tinkwell.Bootstrapper.Hosting;
using Tinkwell.Supervisor;

// Before doing anything else, Let's make sure that the Tinkwell data directories are present
// so that runners and firmlets do not need to worry about that.
Directory.CreateDirectory(HostingInformation.ApplicationDataDirectory);
Directory.CreateDirectory(HostingInformation.UserDataDirectory);

var host = Host.CreateDefaultBuilder(args)
   .AddWorker()
   .ConfigureLogging(logging =>
   {
       logging.ClearProviders();
       logging.AddConsole();
   })
   .Build();

await host.RunAsync();
