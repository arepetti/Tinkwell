namespace Tinkwell.Integration;

/// <summary>
/// Implemented by runlets that want to add CoAP routes programmatically.
/// Register an implementation in <see cref="Microsoft.Extensions.DependencyInjection.IServiceCollection"/>
/// during <c>ConfigureServices</c>; the CoAP runlet discovers all providers
/// at startup and maps their routes alongside <c>.tw</c>-configured resources.
/// </summary>
/// <remarks>
/// Configuration-defined routes take priority over code-defined routes.
/// If both define the same path pattern, the <c>.tw</c> binding runs first.
/// </remarks>
public interface ICoapBindingProvider
{
    /// <summary>
    /// Registers routes on the CoAP server.
    /// </summary>
    void Configure(ICoapRouteBuilder routes);
}

/// <summary>
/// Fluent builder for registering CoAP routes from code.
/// Method names match standard CoAP verbs (RFC 7252, Section 5.8).
/// Path patterns support <c>+</c> (single segment) and <c>#</c> (multi-segment) wildcards.
/// </summary>
public interface ICoapRouteBuilder
{
    /// <summary>Maps a handler for CoAP GET on the given path pattern.</summary>
    ICoapRouteBuilder MapGet(string pattern, ICoapResourceHandler handler);

    /// <summary>Maps a handler for CoAP PUT on the given path pattern.</summary>
    ICoapRouteBuilder MapPut(string pattern, ICoapResourceHandler handler);

    /// <summary>Maps a handler for CoAP POST on the given path pattern.</summary>
    ICoapRouteBuilder MapPost(string pattern, ICoapResourceHandler handler);

    /// <summary>Maps a handler for CoAP DELETE on the given path pattern.</summary>
    ICoapRouteBuilder MapDelete(string pattern, ICoapResourceHandler handler);

    /// <summary>Matches any method on the given path pattern.</summary>
    ICoapRouteBuilder Map(string pattern, ICoapResourceHandler handler);
}
