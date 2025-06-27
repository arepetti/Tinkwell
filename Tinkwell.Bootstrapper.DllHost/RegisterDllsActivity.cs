using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Tinkwell.Bootstrapper.Ensamble;
using Tinkwell.Bootstrapper.Ipc;
using Tinkwell.Bootstrapper.Ipc.Extensions;

namespace Tinkwell.Bootstrapper.DllHost;

sealed class RegisterDllsActivity : IActivity
{
    public RegisterDllsActivity(ILogger<RegisterDllsActivity> logger, IConfiguration configuration, INamedPipeClient pipeClient, IEnsambleConditionEvaluator evaluator)
    {
        _logger = logger;
        _configuration = configuration;
        _pipeClient = pipeClient;
        _evaluator = evaluator;
    }

    public async Task ConfigureBuilderAsync(IHostBuilder builder, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Configuring dependencies for hosted DLLs");
        _dlls ??= await FetchListAsync(cancellationToken);
        foreach (var dll in _dlls)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            _logger.LogDebug("Configuring {Name}", dll.Name);
            var host = new HostProxy(builder)
            {
                RunnerName = dll.Name,
                Properties = dll.Properties
            };

            foreach (var registrar in LoadRegistrarsFrom(dll))
                registrar.ConfigureServices(host);
        }

        _logger.LogInformation("{Name} loaded {Count} runner(s): {Runners}",
            Environment.GetEnvironmentVariable(WellKnownNames.RunnerNameEnvironmentVariable),
            _dlls?.Count(),
            string.Join(',', _dlls!.Select(x => Trim(x.Name))));

        static string Trim(string text)
            => text.Length > 8 ? text.Substring(0, 12) + "..." : text;
    }

    private readonly ILogger _logger;
    private readonly IConfiguration _configuration;
    private readonly INamedPipeClient _pipeClient;
    private readonly IEnsambleConditionEvaluator _evaluator;
    private IEnumerable<HostedDll>? _dlls;

    private IEnumerable<IHostedDllRegistrar> LoadRegistrarsFrom(HostedDll configuration)
    {
        if (configuration.Assembly is null)
        {
            _logger.LogDebug("Loading assembly {Name}", configuration.Name);
            configuration.Assembly = Assembly.LoadFrom(configuration.Path);
        }

        return configuration.Assembly
            .GetExportedTypes()
            .Where(IsInstantiable)
            .Select(CreateInstance);

        static bool IsInstantiable(Type type)
            => typeof(IHostedDllRegistrar).IsAssignableFrom(type) && !type.IsAbstract;

        // Note that registrars CANNOT use DI! This activity has been created using a "shadow host"
        // to circumvent the fact that IHostBuilder does not have a simple way to create synchronously
        // (= not inside ConfigureServices() because it's deferred) an IServiceProvider. To workaround
        // this we create a throw-away host in Extensions.DelegateConfigureServices() to DI this activity
        // but we can't use it safely outside of here because it's disposed (including the services you obtained)
        // right after this task completes.
        IHostedDllRegistrar CreateInstance(Type type)
            => (IHostedDllRegistrar)Activator.CreateInstance(type)!;
    }

    private async Task<IEnumerable<HostedDll>> FetchListAsync(CancellationToken cancellationToken)
    {
        if (_dlls is not null)
            return _dlls;

        var definition = await _pipeClient.SendCommandToSupervisorAndDisconnectAsync<RunnerDefinition>(
            _configuration, $"runners get --pid {Environment.ProcessId}", cancellationToken);

        if (definition.Children.Any(x => x.Activation.Count != 0))
            _logger.LogWarning("One or more children of {Name} have activation requirements which are not supported and they're going to be ignored.", HostingInformation.RunnerName);

        return _evaluator.Filter(definition.Children)
            .Select(x => new HostedDll(x.Name, x.Path, x.Properties))
            .ToArray();
    }

    sealed class HostProxy(IHostBuilder builder) : IDllHost
    {
        public string RunnerName { get; internal set; } = null!;

        public IDictionary<string, object> Properties { get; internal set; } = null!;

        public IHostBuilder ConfigureServices(Action<HostBuilderContext, IServiceCollection> configureDelegate)
            => _builder.ConfigureContainer(configureDelegate);

        private readonly IHostBuilder _builder = builder;
    }

    sealed record HostedDll(string Name, string Path, Dictionary<string, object> Properties)
    {
        public Assembly? Assembly { get; set; }
    }
}
