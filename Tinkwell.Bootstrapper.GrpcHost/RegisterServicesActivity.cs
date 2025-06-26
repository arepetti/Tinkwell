using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Tinkwell.Bootstrapper.Ensamble;
using Tinkwell.Bootstrapper.Ipc;
using Tinkwell.Bootstrapper.Ipc.Extensions;
using Tinkwell.Bootstrapper.Rpc;

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
        var host = new HostProxy(builder, _serviceProvider);
        await ForEachRegistrarAsync(host, x => x.ConfigureServices(host), cancellationToken);

        _logger.LogInformation("{Name} loaded {Count} runner(s): {Runners}",
            Environment.GetEnvironmentVariable(WellKnownNames.RunnerNameEnvironmentVariable),
            _services?.Count(),
            string.Join(',', _services!.Select(x => x.Name)));
    }

    public async Task ConfigureApplication(WebApplication app, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Configuring endpoints for GRPC services");
        var host = new HostProxy(app);
        await ForEachRegistrarAsync(host, x => x.ConfigureRoutes(host), cancellationToken);
    }

    private IServiceProvider? _serviceProvider;
    private readonly ILogger _logger;
    private readonly IConfiguration _configuration;
    private readonly INamedPipeClient _pipeClient;
    private readonly IEnsambleConditionEvaluator _evaluator;
    private IEnumerable<HostedGrpcServer>? _services;

    private async Task ForEachRegistrarAsync(HostProxy host, Action<IHostedGrpcServerRegistrar> action, CancellationToken cancellationToken)
    {
        _services ??= await FetchServiceListAsync(cancellationToken);

        foreach (var service in _services)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            _logger.LogDebug("Configuring {Name}", service.Name);
            foreach (var registrar in LoadRegistrarsFrom(host, service))
                action(registrar);
        }
    }

    private IEnumerable<IHostedGrpcServerRegistrar> LoadRegistrarsFrom(HostProxy host, HostedGrpcServer configuration)
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
            => typeof(IHostedGrpcServerRegistrar).IsAssignableFrom(type) && !type.IsAbstract;

        IHostedGrpcServerRegistrar CreateInstance(Type type)
            => (IHostedGrpcServerRegistrar)ActivatorUtilities.CreateInstance(host.ServiceProvider, type);
    }

    private async Task<IEnumerable<HostedGrpcServer>> FetchServiceListAsync(CancellationToken cancellationToken)
    {
        // TODO: _services should be cached/shared between instances, it's not (even if registered as singleton)
        // because we manually create an instance from a temporary service provider (which is lost) in FromBuilder().
        // The result? We scan the list of runners twice: when calling ConfigureBuilder() and then for ConfigureRoutes().
        // Overall this entire class/mechanism needs some re-thinking!

        if (_services is not null)
            return _services;

        var definition = await _pipeClient.SendCommandToSupervisorAndDisconnectAsync<RunnerDefinition>(
            _configuration, $"runners get --pid {Environment.ProcessId}", cancellationToken);

        return [.. _evaluator.Filter(definition.Children).Select(x => new HostedGrpcServer
        {
            Name = x.Name,
            Path = x.Path
        })];
    }

    sealed class HostProxy : IGrpcServerHost
    {
        private readonly WebApplication? _app;
        private readonly IHostApplicationBuilder? _builder;
        private readonly IServiceProvider? _serviceProvider;

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

        IServiceCollection IGrpcServerHost.Services
        {
            get
            {
                ThrowNotSupportedIfNull(_builder);
                return _builder.Services;
            }
        }

        public void MapGrpcService<TService>() where TService : class
        {
            ThrowNotSupportedIfNull(_app);

            _app.Services.GetRequiredService<IRegistry>().AddGrpcEndpoint<TService>();
            _app.MapGrpcService<TService>();
        }

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
        public Assembly? Assembly { get; set; }
    }
}