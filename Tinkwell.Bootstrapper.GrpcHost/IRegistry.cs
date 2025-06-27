using Tinkwell.Bootstrapper.Ensamble;

namespace Tinkwell.Bootstrapper.GrpcHost;

[Flags]
public enum RegistrySearchMode
{
    Default = 0,
    PartialMatch = 1,
    IgnoreCase = 2,
}

public interface IRegistry
{
    string? LocalAddress { get; set; }

    string? MasterAddress { get; set; }

    public IEnumerable<ServiceDefinition> Services { get; }

    void Validate(ServiceDefinition definition);

    void AddGrpcEndpoint(ServiceDefinition definition);

    void AddGrpcEndpoint<TService>(ServiceDefinition? definition = default);

    ServiceDefinition? Find(string name, RegistrySearchMode options);

    ServiceDefinition? Find(string name)
        => Find(name, RegistrySearchMode.Default);

    IEnumerable<ServiceDefinition> FindAll(string name, RegistrySearchMode options);

    IEnumerable<ServiceDefinition> FindAll(string name)
        => FindAll(name, RegistrySearchMode.PartialMatch | RegistrySearchMode.IgnoreCase);
}
