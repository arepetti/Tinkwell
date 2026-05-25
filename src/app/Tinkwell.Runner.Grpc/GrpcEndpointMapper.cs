using Microsoft.AspNetCore.Routing;
using Tinkwell.Runner;

namespace Tinkwell.Runner.Grpc;

/// <summary>
/// Wraps an <see cref="IEndpointRouteBuilder"/> to map gRPC service endpoints
/// and simultaneously collect <see cref="ServiceDefinition"/> metadata for
/// coordinator-based service discovery.
/// </summary>
internal sealed class GrpcEndpointMapper : IGrpcEndpointMapper
{
    private readonly IEndpointRouteBuilder _endpoints;
    private readonly string _host;
    private readonly string _scheme;
    private readonly List<ServiceDefinition> _registered = [];

    /// <param name="endpoints">The ASP.NET Core endpoint route builder.</param>
    /// <param name="host">
    /// The runner's network host in <c>ip:port</c> form
    /// (e.g. <c>127.0.0.1:4900</c>).
    /// </param>
    /// <param name="tlsMode">
    /// Determines the URL scheme (<c>http</c> vs <c>https</c>) used in
    /// registered <see cref="ServiceDefinition.Url"/> values.
    /// </param>
    public GrpcEndpointMapper(IEndpointRouteBuilder endpoints, string host, TlsMode tlsMode)
    {
        _endpoints = endpoints;
        _host = host;
        _scheme = tlsMode == TlsMode.None ? "http" : "https";
    }

    public IReadOnlyList<ServiceDefinition> RegisteredServices => _registered;

    public ServiceDefinition MapService<TService>() where TService : class
        => MapService<TService>(_ => { });

    public ServiceDefinition MapService<TService>(
        Action<ServiceRegistrationOptions> configure) where TService : class
    {
        GrpcEndpointRouteBuilderExtensions.MapGrpcService<TService>(_endpoints);

        var options = new ServiceRegistrationOptions();
        configure(options);

        string grpcName = GrpcNameResolver.Resolve(typeof(TService));
        var definition = new ServiceDefinition(
            Name: grpcName,
            Type: ServiceType.Grpc,
            FriendlyName: options.FriendlyName,
            FamilyName: options.FamilyName,
            Aliases: options.Aliases.ToList(),
            Host: _host,
            Url: $"{_scheme}://{_host}/{grpcName}");

        _registered.Add(definition);
        return definition;
    }
}
