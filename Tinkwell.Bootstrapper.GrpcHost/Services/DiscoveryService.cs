using Grpc.Core;
using Tinkwell.Bootstrapper.GrpcHost;
using Tinkwell.Services;

namespace Tinkwell.Bootstrapper.Rpc.ServerHost.Services;

public sealed class DiscoveryService(IRegistry registry) : Discovery.DiscoveryBase
{
    public override Task<DiscoveryListReply> List(DiscoveryListRequest request, ServerCallContext context)
    {
        var services = _registry.FindAll(request.Query);

        var result = new DiscoveryListReply();
        result.Services.AddRange(
            services.Select(x => {
                var description = new ServiceDescription()
                {
                    Name = x.Name,
                    Host = x.Host,
                    Url = x.Url
                };

                description.Aliases.AddRange(x.Aliases);

                if (IsAlternateName(description, description.FriendlyName))
                    description.FriendlyName = x.FriendlyName;

                if (IsAlternateName(description, description.FamilyName))
                    description.FamilyName = x.FamilyName;

                return description;
            })
        );

        return Task.FromResult(result);

        bool IsAlternateName(ServiceDescription description, string? altName)
            => altName is not null && !string.Equals(description.Name, altName, StringComparison.Ordinal);
    }

    public override Task<DiscoveryFindReply> Find(DiscoveryFindRequest request, ServerCallContext context)
    {
        var service = _registry.Find(request.Name);

        var result = new DiscoveryFindReply();
        if (service is not null)
            result.Url = service.Url;

        return Task.FromResult(result);
    }

    public override Task<DiscoveryRegisterReply> Register(DiscoveryRegisterRequest request, ServerCallContext context)
    {
        // Note: this is supposed to be called only by another executable hosting GRPC services (maybe from another
        // machine). We need only one Discovery service then the first one (by configuration) keeps the list.
        try
        {
            var definition = new ServiceDefinition()
            {
                Name = request.Service.Name,
                FriendlyName = request.Service.FriendlyName,
                FamilyName = request.Service.FamilyName,
                Aliases = request.Service.Aliases.ToArray(),
                Host = request.Service.Host,
                Url = request.Service.Url,
            };

            _registry.AddGrpcEndpoint(definition);

            return Task.FromResult(new DiscoveryRegisterReply());
        }
        catch (ArgumentException e)
        {
            throw new RpcException(new Status(StatusCode.AlreadyExists, e.Message));
        }
    }

    private readonly IRegistry _registry = registry;
}
