namespace Tinkwell.Runner;

/// <summary>
/// The transport protocol of a registered service.
/// </summary>
public enum ServiceType
{
    /// <summary>A gRPC service over HTTP/2.</summary>
    Grpc,

    /// <summary>An HTTP/REST API service.</summary>
    Api
}
