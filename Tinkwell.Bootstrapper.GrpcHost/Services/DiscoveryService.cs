using Grpc.Core;
using Tinkwell.Bootstrapper.Ensamble;
using Tinkwell.Bootstrapper.GrpcHost;
using Tinkwell.Services;

namespace Tinkwell.Bootstrapper.Rpc.ServerHost.Services;

public sealed class DiscoveryService(IRegistry registry) : Discovery.DiscoveryBase
{
    public override Task<DiscoveryListReply> List(DiscoveryListRequest request, ServerCallContext context)
    {
        return ExecuteWithErrorHandling(() =>
        {
            var services = _registry.FindAll(request.Query);
            var result = new DiscoveryListReply();
            result.Services.AddRange(services.Select(DefinitionToDescription));
            return result;
        });

        static ServiceDescription DefinitionToDescription(ServiceDefinition definition)
        {
            var description = new ServiceDescription()
            {
                Name = definition.Name,
                Host = definition.Host,
                Url = definition.Url
            };

            description.Aliases.AddRange(definition.Aliases);

            if (!string.IsNullOrWhiteSpace(definition.FriendlyName))
                description.FriendlyName = definition.FriendlyName;

            if (!string.IsNullOrWhiteSpace(definition.FamilyName))
                description.FamilyName = definition.FamilyName;

            return description;
        }
    }

    public override Task<DiscoveryFindReply> Find(DiscoveryFindRequest request, ServerCallContext context)
    {
        return ExecuteWithErrorHandling(() =>
        {
            var service = _registry.Find(request.Name);

            var result = new DiscoveryFindReply();
            if (service is not null)
            {
                result.Url = service.Url;
                result.Host = service.Host;
            }

            return result;
        });
    }

    public override Task<DiscoveryFindAllReply> FindAll(DiscoveryFindAllRequest request, ServerCallContext context)
    {
        if (string.IsNullOrWhiteSpace(request.FamilyName))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Family name is required."));
     
        return ExecuteWithErrorHandling(() =>
        {
            var result = new DiscoveryFindAllReply();
            var hosts = _registry
                .FindAll(x => string.Equals(x.FamilyName, request.FamilyName, StringComparison.Ordinal))
                .Select(x => x.Host);

            result.Hosts.AddRange(hosts);
            return result;
        });
    }

    public override Task<DiscoveryRegisterReply> Register(DiscoveryRegisterRequest request, ServerCallContext context)
    {
        // Note: this is supposed to be called only by another executable hosting GRPC services (maybe from another
        // machine). We need only one Discovery service then the first one (by configuration) keeps the list.
        return ExecuteWithErrorHandling(() =>
        {
            var definition = new ServiceDefinition()
            {
                Name = request.Service.Name,
                FriendlyName = request.Service.FriendlyName,
                FamilyName = request.Service.FamilyName,
                Aliases = [.. request.Service.Aliases],
                Host = request.Service.Host,
                Url = request.Service.Url,
            };

            _registry.AddGrpcEndpoint(definition);

            return new DiscoveryRegisterReply();
        });
    }

    private readonly IRegistry _registry = registry;

    private static Task<T> ExecuteWithErrorHandling<T>(Func<T> action)
    {
        try
        {
            return Task.FromResult(action());
        }
        catch (DuplicateNameException e)
        {
            throw new RpcException(new Status(StatusCode.AlreadyExists, e.Message));
        }
        catch (ArgumentException e)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, e.Message));
        }
        catch (Exception e) when (e is not RpcException)
        {
            throw new RpcException(new Status(StatusCode.Internal, e.Message));
        }
    }
}
