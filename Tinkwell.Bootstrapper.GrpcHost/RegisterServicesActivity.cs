using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Tinkwell.Bootstrapper.Ensamble;
using Tinkwell.Bootstrapper.Hosting;
using Tinkwell.Bootstrapper.Ipc;
using Tinkwell.Bootstrapper.Ipc.Extensions;

namespace Tinkwell.Bootstrapper.GrpcHost;

sealed class RegisterServicesActivity : IActivity
{
    public RegisterServicesActivity(ILogger<RegisterServicesActivity> logger, IConfiguration configuration, INamedPipeClient pipeClient, IEnsambleConditionEvaluator evaluator)
    {
        _logger = logger;
        _configuration = configuration;
        _pipeClient = pipeClient;
        _evaluator = evaluator;
    }

    public static RegisterServicesActivity FromBuilder(IHostApplicationBuilder builder)
    {
        var serviceProvider = builder.Services.BuildServiceProvider();
        var activity = ActivatorUtilities.CreateInstance<RegisterServicesActivity>(serviceProvider);
        activity._serviceProvider = serviceProvider;
        return activity;
    }

    public async Task ConfigureBuilder(IHostApplicationBuilder builder, CancellationToken cancellationToken)
    {
        Debug.Assert(_serviceProvider is not null);

        _logger.LogDebug("Configuring dependencies for GRPC services ");
        await ForEachRegistrarAsync(
            () => new HostProxy(builder, _serviceProvider),
            (host, registrar) => registrar.ConfigureServices(host),
            cancellationToken);

        _logger.LogInformation("{Name} loaded {Count} runner(s): {Runners}",
            Environment.GetEnvironmentVariable(WellKnownNames.RunnerNameEnvironmentVariable),
            _services?.Count(),
            string.Join(',', _services!.Select(x => Trim(x.Name))));

        static string Trim(string text)
            => text.Length > 20 ? text.Substring(0, 20) + "..." : text;
    }

    public async Task ConfigureApplication(WebApplication app, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Configuring endpoints for GRPC services");
        await ForEachRegistrarAsync(
            () => new HostProxy(app),
            (host, registrar) =>
            {
                if (registrar is IHostedGrpcServerRegistrar grpcRegistrar)
                    grpcRegistrar.ConfigureRoutes(host);
            },
            cancellationToken);
    }

    private IServiceProvider? _serviceProvider;
    private readonly ILogger _logger;
    private readonly IConfiguration _configuration;
    private readonly INamedPipeClient _pipeClient;
    private readonly IEnsambleConditionEvaluator _evaluator;
    private IEnumerable<HostedGrpcServer>? _services;

    private async Task ForEachRegistrarAsync(Func<HostProxy> hostFactory, Action<HostProxy, IHostedAssemblyRegistrar> action, CancellationToken cancellationToken)
    {
        _services ??= await FetchServiceListAsync(cancellationToken);

        foreach (var service in _services)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            _logger.LogDebug("Configuring {Name}", service.Name);
            var host = hostFactory();
            host.RunnerName = service.Name;
            host.Properties = service.Properties;
            foreach (var registrar in LoadRegistrarsFrom(host, service))
                action(host, registrar);
        }
    }

    private IEnumerable<IHostedAssemblyRegistrar> LoadRegistrarsFrom(HostProxy host, HostedGrpcServer configuration)
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
            => (IHostedAssemblyRegistrar)ActivatorUtilities.CreateInstance(host.ServiceProvider, type);
    }

    private async Task<IEnumerable<HostedGrpcServer>> FetchServiceListAsync(CancellationToken cancellationToken)
    {
        if (_services is not null)
            return _services;

        var definition = await _pipeClient.SendCommandToSupervisorAndDisconnectAsync<RunnerDefinition>(
            _configuration, $"runners get --pid {Environment.ProcessId}", cancellationToken);

        if (definition.Children.Any(x => x.Activation.Count != 0))
            _logger.LogWarning("One or more children of {Name} have activation requirements which are not supported and they're going to be ignored.", HostingInformation.RunnerName);

        return [.. _evaluator.Filter(definition.Children).Select(x => {
            var path = Path.IsPathRooted(x.Path)
            ? x.Path
            : Path.Combine(StrategyAssemblyLoader.GetAppPath(), x.Path);

            return new HostedGrpcServer
            {
                Name = x.Name,
                Path = path,
                Properties = x.Properties,
            };
        })];
    }

    sealed class HostProxy : IGrpcServerHost
    {
        public HostProxy(WebApplication app)
        {
            _app = app;
        }

        public HostProxy(IHostApplicationBuilder builder, IServiceProvider serviceProvider)
        {
            _builder = builder;
            _serviceProvider = serviceProvider;
        }

        public IServiceProvider ServiceProvider
            => _app?.Services ?? _serviceProvider!;

        public string RunnerName { get; internal set; } = null!;

        public IDictionary<string, object> Properties { get; internal set; } = null!;

        public void ConfigureServices(Action<HostBuilderContext, IServiceCollection> configureDelegate)
        {
            ThrowNotSupportedIfNull(_builder);
            configureDelegate(new HostBuilderContext(_builder.Properties), _builder.Services);
        }

        public void MapGrpcService<TService>(ServiceDefinition? definition = default) where TService : class
        {
            ThrowNotSupportedIfNull(_app);

            _app.Services.GetRequiredService<IRegistry>().AddGrpcEndpoint<TService>(definition);
            _app.MapGrpcService<TService>();
        }

        private readonly WebApplication? _app;
        private readonly IHostApplicationBuilder? _builder;
        private readonly IServiceProvider? _serviceProvider;

        private static void ThrowNotSupportedIfNull([NotNull] object? value)
        {
            if (value is null)
                throw new NotSupportedException("This operation is not allowed at this stage.");
        }
    }

    sealed class HostedGrpcServer
    {
        public required string Name { get; set; }
        public required string Path { get; init; }
        public required Dictionary<string, object> Properties { get; init; }
        public Assembly? Assembly { get; set; }
    }
}
