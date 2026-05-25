namespace Tinkwell;

/// <summary>
/// Controls whether TLS is used for gRPC communication and how
/// certificates are validated on the client side.
/// </summary>
public enum TlsMode
{
    /// <summary>
    /// Plain HTTP/2 cleartext (H2C). No certificates, no encryption.
    /// Default for development and single-machine deployments.
    /// </summary>
    None,

    /// <summary>
    /// HTTPS with a self-signed certificate. The server presents the
    /// certificate and clients skip server certificate validation.
    /// </summary>
    SelfSigned,

    /// <summary>
    /// HTTPS with a CA-signed (trusted) certificate. The server presents
    /// the certificate and clients validate it normally through the OS
    /// trust store.
    /// </summary>
    Standard
}
