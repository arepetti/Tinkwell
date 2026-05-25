namespace Tinkwell.Package;

/// <summary>
/// Options for <see cref="TwPackage.UnpackAsync(string, string, UnpackOptions?, System.Threading.CancellationToken)"/>.
/// </summary>
public sealed class UnpackOptions
{
    /// <summary>Verify the package before extraction. Default: <c>true</c>.</summary>
    public bool Verify { get; set; } = true;

    /// <summary>
    /// Trusted publisher public keys (X.509 <c>SubjectPublicKeyInfo</c>) used
    /// when <see cref="Verify"/> is <c>true</c>.
    /// </summary>
    public IReadOnlyList<byte[]> TrustedKeys { get; set; } = Array.Empty<byte[]>();

    /// <summary>
    /// Single-key shortcut. Prefer <see cref="TrustedKeys"/>.
    /// </summary>
    [Obsolete("Use TrustedKeys. Kept to avoid churn in existing callers during the GA transition.")]
    public byte[]? PublicKey { get; set; }

    /// <summary>Whether signatures are required. Default: <c>true</c>.</summary>
    public bool RequireSignatures { get; set; } = true;

    /// <summary>
    /// Explicitly accept verification against file hashes only, without
    /// validating the ECDSA signature. See
    /// <see cref="VerifyOptions.AllowIntegrityOnly"/>. Default: <c>false</c>.
    /// </summary>
    public bool AllowIntegrityOnly { get; set; } = false;

    /// <summary>Maximum total decompressed size in bytes. Default: 256 MB.</summary>
    public long MaxDecompressedSize { get; set; } = 256 * 1024 * 1024;

    /// <summary>Maximum single file size in bytes. Default: 64 MB.</summary>
    public long MaxFileSize { get; set; } = 64 * 1024 * 1024;

    /// <summary>Maximum number of entries in the package. Default: 10,000.</summary>
    public int MaxFileCount { get; set; } = 10_000;

    /// <summary>Maximum path length for entries. Default: 260.</summary>
    public int MaxPathLength { get; set; } = 260;

    /// <summary>
    /// When <c>true</c> and <see cref="Verify"/> is <c>true</c>, the extracted
    /// content is also checked for standard package layout. Default: <c>false</c>.
    /// </summary>
    public bool StrictLayout { get; set; } = false;
}
