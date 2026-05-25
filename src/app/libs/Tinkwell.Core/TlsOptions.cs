namespace Tinkwell;

/// <summary>
/// TLS configuration for gRPC communication, bound from the <c>Tls</c>
/// section of <c>appsettings.json</c>.
/// </summary>
public sealed class TlsOptions
{
    /// <summary>
    /// The TLS mode. Defaults to <see cref="TlsMode.None"/> (H2C).
    /// </summary>
    public TlsMode Mode { get; set; } = TlsMode.None;

    /// <summary>
    /// Path to the certificate file (<c>.pfx</c>) used by Kestrel for
    /// HTTPS. Required when <see cref="Mode"/> is not
    /// <see cref="TlsMode.None"/>.
    /// </summary>
    public string CertificatePath { get; set; } = "";

    /// <summary>Optional password for the PFX/PKCS#12 certificate file.</summary>
    public string? CertificatePassword { get; set; }

    /// <summary>
    /// Whether TLS is enabled (any mode other than <see cref="TlsMode.None"/>).
    /// </summary>
    public bool IsEnabled => Mode != TlsMode.None;

    /// <summary>
    /// The URL scheme appropriate for this TLS mode.
    /// </summary>
    public string Scheme => IsEnabled ? "https" : "http";
}
