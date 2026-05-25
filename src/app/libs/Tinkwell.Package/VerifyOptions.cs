namespace Tinkwell.Package;

/// <summary>
/// Options for <see cref="TwPackage.VerifyAsync(string, VerifyOptions?, System.Threading.CancellationToken)"/>.
/// </summary>
/// <remarks>
/// Verification is strict by default: unless <see cref="AllowIntegrityOnly"/>
/// is set, <see cref="TrustedKeys"/> (or the obsolete <see cref="PublicKey"/>
/// shortcut) MUST contain at least one key whenever
/// <see cref="RequireSignatures"/> is <c>true</c>. This protects against
/// forged <c>signatures.tw</c> manifests that claim integrity-clean hashes
/// for tampered content.
/// </remarks>
public sealed class VerifyOptions
{
    /// <summary>
    /// Trusted publisher public keys encoded as X.509
    /// <c>SubjectPublicKeyInfo</c>. Verification succeeds if the package
    /// signature was produced by the private key matching any one of them.
    /// </summary>
    public IReadOnlyList<byte[]> TrustedKeys { get; set; } = Array.Empty<byte[]>();

    /// <summary>
    /// Single-key shortcut. When set, behaves like a one-entry
    /// <see cref="TrustedKeys"/> collection (merged with any explicitly
    /// configured entries). Prefer <see cref="TrustedKeys"/>.
    /// </summary>
    [Obsolete("Use TrustedKeys. Kept to avoid churn in existing callers during the GA transition.")]
    public byte[]? PublicKey { get; set; }

    /// <summary>
    /// Whether <c>signatures.tw</c> and <c>signature.sig</c> must be present
    /// in the package. When <c>true</c>, ECDSA verification is performed if
    /// <see cref="TrustedKeys"/> are provided; otherwise the caller must set
    /// <see cref="AllowIntegrityOnly"/> to opt into hash-only checks.
    /// Default: <c>true</c>.
    /// </summary>
    public bool RequireSignatures { get; set; } = true;

    /// <summary>
    /// Explicitly accept verification against file hashes only, without
    /// validating the ECDSA signature over <c>signatures.tw</c>. Intended for
    /// local development and diagnostic tools. Default: <c>false</c>.
    /// </summary>
    /// <remarks>
    /// Integrity-only verification CANNOT detect an attacker who rewrites
    /// <c>signatures.tw</c> to match tampered content. Do not use this mode
    /// for untrusted packages.
    /// </remarks>
    public bool AllowIntegrityOnly { get; set; } = false;

    /// <summary>
    /// When <c>true</c>, enforces the standard package layout
    /// (<c>package.tw</c>, <c>content/</c>, <c>security/</c> only). Default: <c>false</c>
    /// for backward compatibility.
    /// </summary>
    public bool StrictLayout { get; set; } = false;
}
