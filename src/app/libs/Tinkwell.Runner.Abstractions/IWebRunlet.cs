namespace Tinkwell.Runner;

/// <summary>
/// Contract for runlets hosted inside a web runner that exposes HTTP/REST endpoints.
/// In addition to DI registration via <see cref="IRunlet.ConfigureServices"/>,
/// web runlets map their endpoints into the runner's shared routing pipeline.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="MapEndpoints"/> is called after the host is built but before it
/// starts listening. <see cref="IWebEndpointMapper"/> only exposes route-mapping
/// helpers (it does not perform coordinator service registration). Named services
/// for discovery are published through the runner's transport-specific pipeline
/// (e.g. gRPC mapping collecting <see cref="ServiceDefinition"/> for
/// <c>service register</c>), not through this HTTP mapper.
/// </para>
/// <para>
/// Loading a runlet that implements only <see cref="IRunlet"/> (not
/// <see cref="IWebRunlet"/>) into a web runner is a configuration error
/// and will cause the runner to send <c>notify fatal</c>.
/// </para>
/// </remarks>
public interface IWebRunlet : IRunlet
{
    /// <summary>
    /// Maps this runlet's HTTP endpoints. Publishing named services to the
    /// coordinator (if the runner supports it) is separate from
    /// <see cref="IWebEndpointMapper"/>, which only wraps route registration.
    /// </summary>
    /// <param name="mapper">
    /// The endpoint mapper that surfaces ASP.NET Core's minimal-API style
    /// route registration without a direct hosting dependency in this package.
    /// </param>
    void MapEndpoints(IWebEndpointMapper mapper);
}
