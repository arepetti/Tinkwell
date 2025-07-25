using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using Grpc.Core;
using Grpc.Net.Client;
using Tinkwell.Bootstrapper.Hosting;
using Tinkwell.Services;

namespace Tinkwell.Bootstrapper.GrpcHost;

sealed class Registry(IConfiguration configuration) : IRegistry
{
    public string? LocalAddress { get; set; }

    public string? MasterAddress { get; set; }

    IEnumerable<ServiceDefinition> IRegistry.Services
        => _services;

    public void Validate(ServiceDefinition definition)
    {
        if (string.IsNullOrWhiteSpace(definition.Name))
            throw new ArgumentException("Service name cannot be empty.");

        if (string.IsNullOrWhiteSpace(definition.Url))
            throw new ArgumentException("Service URL cannot be empty.");

        if (Exists(definition.Name))
            throw new DuplicateNameException($"Another service with the same name '{definition.Name}' exists.");

        if (definition.Aliases is not null && definition.Aliases.Any(x => Exists(x)))
            throw new DuplicateNameException($"Another service with the same alias exists.");
    }

    public void AddGrpcEndpoint(ServiceDefinition definition)
    {
        // TODO: add locking here, someone else might add a service after Validate() but before
        // we add the object to the collection.
        Validate(definition);
        _services.Add(definition);
    }

    public void AddGrpcEndpoint<TService>(ServiceDefinition? definition)
    {
        string name = definition?.Name ?? GetServiceFullName(typeof(TService));
        var finalDefinition = new ServiceDefinition()
        {
            Name = name,
            FriendlyName = definition?.FriendlyName,
            FamilyName = definition?.FamilyName,
            Aliases = definition?.Aliases ?? [],
            Host = definition?.Host ?? LocalAddress,
            Url = definition?.Url ?? $"{LocalAddress}/{name}",
        };

        // This method is called only when registering a new service, if we're a slave host
        // then we ALSO need to register this service into the master to make it discoverable.
        // We map the endpoint only after calling the master (if present) because the service
        // name might be used already and we do not keep the full list (nor we want to query for one!).
        TryRegisterWithMaster(finalDefinition);
        AddGrpcEndpoint(finalDefinition);
    }

    public ServiceDefinition? Find(string name, RegistrySearchMode mode)
    {
        return FindByName() ?? FindByAlias() ?? FindByFamilyName();

        ServiceDefinition? FindByName()
            => _services.FirstOrDefault(s => Match(s.Name, name, mode));

        ServiceDefinition? FindByAlias()
            => _services.FirstOrDefault(s => s.Aliases.Any(a => Match(a, name, mode)));

        ServiceDefinition? FindByFamilyName()
            => _services.FirstOrDefault(s => Match(s.FamilyName, name, mode));
    }

    public IEnumerable<ServiceDefinition> FindAll(string name, RegistrySearchMode mode)
    {
        return _services.Where(service =>
        {
            if (string.IsNullOrWhiteSpace(name))
                return true;

            return
                Match(service.Name, name, mode)
                || Match(service.FamilyName, name, mode)
                || service.Aliases.Any(alias => Match(alias, name, mode));
        });
    }

    public IEnumerable<ServiceDefinition> FindAll(Func<ServiceDefinition, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate, nameof(predicate));
        return _services.Where(predicate);
    }

    public bool Exists(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return false;

        return Find(name, RegistrySearchMode.Default) is not null;
    }

    private readonly IConfiguration _configuration = configuration;
    private readonly ConcurrentBag<ServiceDefinition> _services = new();
    private GrpcChannel? _grpcChannel;

    // Resolve the full name qualified gRPC name of a gRPC server class.
    private static string GetServiceFullName(Type serviceType)
    {
        var serviceBaseType = serviceType.BaseType;
        ThrowIfNull(serviceBaseType);

        var binding = serviceBaseType.GetCustomAttribute<Grpc.Core.BindServiceMethodAttribute>();
        ThrowIfNull(binding);

        var serviceDefinitionType = binding.BindType;
        ThrowIfNull(binding);

        var descriptorProperty = serviceDefinitionType
            .GetProperty(nameof(Discovery.Descriptor), BindingFlags.Public | BindingFlags.Static);
        ThrowIfNull(descriptorProperty);

        var nameProperty = descriptorProperty.PropertyType
            .GetProperty(nameof(Google.Protobuf.Reflection.ServiceDescriptor.FullName));

        ThrowIfNull(nameProperty);

        var descriptor = descriptorProperty.GetValue(null);
        ThrowIfNull(descriptor);

        return Convert.ToString(nameProperty.GetValue(descriptor), CultureInfo.InvariantCulture)!;

        void ThrowIfNull([NotNull] object? value)
        {
            if (value is null)
                throw new NotSupportedException($"Type {serviceType.FullName} is not a compatible GRPC service.");
        }
    }

    // String matching with options. Determines whether "value" is a match for the search string textToSearch,
    // given the search options specified in "mode".
    private static bool Match(string? value, string? textToSearch, RegistrySearchMode mode)
    {
        // Note this: null does not match another null, it means that the value is unspecified!
        if (value is null || textToSearch is null)
            return false;

        bool ignoreCase = mode.HasFlag(RegistrySearchMode.IgnoreCase);
        var comparison = ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

        if (mode.HasFlag(RegistrySearchMode.PartialMatch))
            return value.Contains(textToSearch, comparison);

        return string.Equals(value, textToSearch, comparison);
    }

    // Tries to register the specified service to the master Discovery. No action is performed if
    // this is the master instance.
    private void TryRegisterWithMaster(ServiceDefinition definition)
    {
        if (string.IsNullOrWhiteSpace(MasterAddress))
            return;

        _grpcChannel ??= GrpcChannel.ForAddress(MasterAddress);
        var client = new Discovery.DiscoveryClient(_grpcChannel);

        try
        {
            ServiceDescription service = new()
            {
                Name = definition.Name,
                Url = definition.Url,
                Host = definition.Host,
                Aliases = { definition.Aliases }
            };

            if (!string.IsNullOrWhiteSpace(definition.FamilyName))
                service.FamilyName = definition.FamilyName;

            if (!string.IsNullOrWhiteSpace(definition.FriendlyName))
                service.FriendlyName = definition.FriendlyName;

            client.Register(new DiscoveryRegisterRequest { Service = service });
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.AlreadyExists)
        {
            // The service is already registered, we throw the same exception thrown
            // by Validate() for a local duplicate.
            throw new ArgumentException($"Another service with the same name '{definition.Name}' exists.");
        }
    }
}