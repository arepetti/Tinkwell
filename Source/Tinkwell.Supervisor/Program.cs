using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Tinkwell.Bootstrapper;
using Tinkwell.Bootstrapper.Hosting;
using Tinkwell.Supervisor;

// Before doing anything else, Let's make sure that the Tinkwell data directories are present
// so that runners and firmlets do not need to worry about that.
Directory.CreateDirectory(HostingInformation.ApplicationDataDirectory);
Directory.CreateDirectory(HostingInformation.UserDataDirectory);

// External plugins have a chance to perform some initializations.
var initializers = StrategyAssemblyLoader
    .LoadAssemblies(typeof(IRegistry).Namespace!, "Init")
    .SelectMany(StrategyAssemblyLoader.FindTypesImplementing<IStrategyImplementationResolver>)
    .Select(Activator.CreateInstance)
    .Cast<IStrategyImplementationResolver>()
    .Select(x => x.GetImplementationType(typeof(IApplicationInitializer)))
    .Where(x => x is not null)
    .Select(x => (IApplicationInitializer)Activator.CreateInstance(x!)!);

foreach (var initializer in initializers)
    await initializer.InitializeAsync(CancellationToken.None);

var host = Host.CreateDefaultBuilder(args)
   .AddWorker()
   .ConfigureLogging(logging =>
   {
       logging.ClearProviders();
       logging.AddSimpleConsole(options =>
       {
           options.SingleLine = true;
           options.TimestampFormat = "HH:mm:ss ";
           options.IncludeScopes = false;
       });
   })
   .Build();

await host.RunAsync();
