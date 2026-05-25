namespace Tinkwell.Package;

/// <summary>
/// Result of a package verification operation.
/// </summary>
public sealed record VerificationResult(
    bool IsValid,
    IReadOnlyList<VerificationIssue> Issues);

/// <summary>
/// A single verification issue found in a package.
/// </summary>
public sealed record VerificationIssue(
    VerificationSeverity Severity,
    string Code,
    string Message,
    string? FilePath = null);

/// <summary>Whether a verification <see cref="VerificationIssue"/> is advisory or blocks validity.</summary>
public enum VerificationSeverity
{
    /// <summary>Non-fatal: included in <see cref="VerificationResult.Issues"/> but does not set <see cref="VerificationResult.IsValid"/> to <c>false</c> by itself.</summary>
    Warning,

    /// <summary>Fatal: contributes to an invalid <see cref="VerificationResult"/>.</summary>
    Error
}

/// <summary>
/// Well-known verification issue codes.
/// </summary>
public static class VerificationCodes
{
    /// <summary><c>package.tw</c> is missing at the package root.</summary>
    public const string MissingManifest = "MISSING_MANIFEST";

    /// <summary><c>security/signatures.tw</c> is missing when signatures are required.</summary>
    public const string MissingSignatures = "MISSING_SIGNATURES";

    /// <summary><c>security/signature.sig</c> is missing when signatures are required.</summary>
    public const string MissingSignature = "MISSING_SIGNATURE";

    /// <summary>ECDSA verification failed: no <see cref="VerifyOptions.TrustedKeys"/> entry matched the signature over <c>signatures.tw</c>.</summary>
    public const string InvalidSignature = "INVALID_SIGNATURE";

    /// <summary>File content SHA-512 does not match the signed hash.</summary>
    public const string HashMismatch = "HASH_MISMATCH";

    /// <summary>File length does not match the signed size.</summary>
    public const string SizeMismatch = "SIZE_MISMATCH";

    /// <summary>A file exists in the package but is not listed in <c>signatures.tw</c> (except under <c>security/</c>).</summary>
    public const string UndeclaredFile = "UNDECLARED_FILE";

    /// <summary>A path listed in <c>signatures.tw</c> is missing on disk, or a completeness check failed for a missing file.</summary>
    public const string MissingFile = "MISSING_FILE";

    /// <summary>Signature entries in <c>signatures.tw</c> are not in canonical (strict ascending) order.</summary>
    public const string NonCanonicalOrder = "NON_CANONICAL_ORDER";

    /// <summary>Package layout violates allowed structure, for example when <see cref="VerifyOptions.StrictLayout"/> is enabled.</summary>
    public const string InvalidStructure = "INVALID_STRUCTURE";

    /// <summary>Manifest or security files could not be parsed, or exceed size limits.</summary>
    public const string ParseError = "PARSE_ERROR";

    /// <summary>
    /// The signatures manifest algorithm label does not match the built-in verifier.
    /// </summary>
    public const string UnknownAlgorithm = "UNKNOWN_ALGORITHM";

    /// <summary>
    /// Informational: the package was not verified cryptographically because
    /// the caller opted into <see cref="VerifyOptions.AllowIntegrityOnly"/>.
    /// </summary>
    public const string IntegrityOnly = "INTEGRITY_ONLY";

    /// <summary>
    /// Informational: no signature files are present in the package and the
    /// caller accepted this via <c>RequireSignatures = false</c>.
    /// </summary>
    public const string Unsigned = "UNSIGNED";
}
