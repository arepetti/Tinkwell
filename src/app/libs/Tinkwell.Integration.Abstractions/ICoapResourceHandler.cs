namespace Tinkwell.Integration;

/// <summary>
/// Handles a CoAP request for a code-defined route registered via
/// <see cref="ICoapBindingProvider"/>. Unlike <see cref="IIntegrationBinding"/>,
/// this handler is not driven by <c>.tw</c> configuration — it receives the
/// raw <see cref="IntegrationContext"/> and decides how to respond.
/// </summary>
public interface ICoapResourceHandler
{
    /// <summary>
    /// Processes the request and optionally returns a response body.
    /// </summary>
    Task<BindingResult?> HandleAsync(IntegrationContext context, CancellationToken ct);
}
