namespace Tinkwell.Runner;

/// <summary>
/// Maps HTTP/REST endpoints into the runner's routing pipeline.
/// Passed to <see cref="IWebRunlet.MapEndpoints"/> instead of a raw
/// <c>object</c>, providing a clean abstraction over <c>IEndpointRouteBuilder</c>
/// without requiring an ASP.NET Core dependency in the abstractions package.
/// </summary>
/// <remarks>
/// The route mapping methods do not return the ASP.NET <c>RouteHandlerBuilder</c>
/// to avoid pulling ASP.NET Core into the abstractions package. Advanced fluent
/// configuration can be supported in the future via a richer return type or an
/// escape hatch.
/// </remarks>
public interface IWebEndpointMapper
{
    /// <summary>Maps a GET endpoint.</summary>
    void MapGet(string pattern, Delegate handler);

    /// <summary>Maps a POST endpoint.</summary>
    void MapPost(string pattern, Delegate handler);

    /// <summary>Maps a PUT endpoint.</summary>
    void MapPut(string pattern, Delegate handler);

    /// <summary>Maps a DELETE endpoint.</summary>
    void MapDelete(string pattern, Delegate handler);

    /// <summary>Maps a PATCH endpoint.</summary>
    void MapPatch(string pattern, Delegate handler);

    /// <summary>
    /// Creates a route group with the specified prefix. All endpoints mapped
    /// through the returned mapper share the prefix.
    /// </summary>
    IWebEndpointMapper MapGroup(string prefix);
}
