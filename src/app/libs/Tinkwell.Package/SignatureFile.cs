namespace Tinkwell.Package;

/// <summary>
/// Parsed content of a <c>security/signature.sig</c> file. Contains the
/// algorithm, key fingerprint, timestamp, and the raw signature bytes.
/// </summary>
public sealed record SignatureFile
{
    /// <summary>ECDSA metadata label (e.g. <see cref="PackageSigner.AlgorithmName"/>).</summary>
    public required string Algorithm { get; init; }

    /// <summary>Key fingerprint for the signing public key (from <see cref="PackageSigner.ComputeKeyId"/>).</summary>
    public required string KeyId { get; init; }

    /// <summary>UTC time when the package was signed.</summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>Raw ECDSA signature bytes over the SHA-512 digest of <c>signatures.tw</c>.</summary>
    public required byte[] SignatureBytes { get; init; }
}
