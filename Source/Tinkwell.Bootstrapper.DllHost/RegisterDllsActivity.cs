using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Tinkwell.Bootstrapper.Ensamble;
using Tinkwell.Bootstrapper.Hosting;
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
            => text.Length > 20 ? text.Substring(0, 20) + "..." : text;
    }

    private readonly ILogger _logger;
    private readonly IConfiguration _configuration;
    private readonly INamedPipeClient _pipeClient;
    private readonly IEnsambleConditionEvaluator _evaluator;
    private IEnumerable<HostedDll>? _dlls;

    private IEnumerable<IHostedAssemblyRegistrar> LoadRegistrarsFrom(HostedDll configuration)
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
            => typeof(IHostedAssemblyRegistrar).IsAssignableFrom(type) && !type.IsAbstract;

        IHostedAssemblyRegistrar CreateInstance(Type type)
            => (IHostedAssemblyRegistrar)Activator.CreateInstance(type)!;
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
            .Select(x => new HostedDll(x.Name, ResolvePath(x.Path), x.Properties))
            .ToArray();

        static string ResolvePath(string path)
            => Path.IsPathRooted(path) ? path : Path.Combine(StrategyAssemblyLoader.GetAppPath(), path);
    }

    private sealed class HostProxy(IHostBuilder builder) : IConfigurableHost
    {
        public required string RunnerName { get; internal init; }

        public required IDictionary<string, object> Properties { get; internal init; }

        public void ConfigureServices(Action<HostBuilderContext, IServiceCollection> configureDelegate)
            => _builder.ConfigureServices(configureDelegate);

        private readonly IHostBuilder _builder = builder;
    }

    private sealed record HostedDll(string Name, string Path, Dictionary<string, object> Properties)
    {
        public Assembly? Assembly { get; set; }
    }
}
