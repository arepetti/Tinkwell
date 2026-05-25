using System.Security.Cryptography;

namespace Tinkwell.Package;

/// <summary>
/// ECDSA P-384 signing and verification for package signatures.
/// The signature is computed over a SHA-512 digest of <c>signatures.tw</c>.
/// Per-file hashes inside <c>signatures.tw</c> are also SHA-512
/// (see <see cref="PackageHasher"/>).
/// </summary>
public static class PackageSigner
{
    /// <summary>
    /// Canonical algorithm identifier written into new <c>signature.sig</c>
    /// files.
    /// </summary>
    public const string AlgorithmName = "ecdsa-p384-sha512";

    /// <summary>
    /// Legacy algorithm identifier. Early pre-GA builds used SHA-512 in the
    /// code but wrote this (incorrect) label into <c>signature.sig</c>.
    /// <see cref="Verify(byte[], SignatureFile, byte[])"/> still accepts it so
    /// those artifacts keep verifying.
    /// </summary>
    public const string LegacyAlgorithmName = "ecdsa-p384-sha384";

    /// <summary>
    /// Generates a new ECDSA P-384 key pair.
    /// </summary>
    /// <returns>PKCS#8 private key and X.509 SubjectPublicKeyInfo public key.</returns>
    public static (byte[] PrivateKey, byte[] PublicKey) GenerateKeyPair()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP384);
        return (ecdsa.ExportPkcs8PrivateKey(), ecdsa.ExportSubjectPublicKeyInfo());
    }

    /// <summary>
    /// Computes a SHA-256 fingerprint of the public key for the <c>key-id</c> field.
    /// </summary>
    /// <param name="publicKey">X.509 SubjectPublicKeyInfo-encoded ECDSA P-384 public key.</param>
    public static string ComputeKeyId(byte[] publicKey)
    {
        ArgumentNullException.ThrowIfNull(publicKey);
        var hash = SHA256.HashData(publicKey);
        return "SHA256:" + Convert.ToHexStringLower(hash[..16]);
    }

    /// <summary>
    /// Signs the SHA-512 hash of <paramref name="signaturesContent"/> with the
    /// caller-supplied ECDSA P-384 private key and produces a
    /// <see cref="SignatureFile"/> tagged with <see cref="AlgorithmName"/>.
    /// </summary>
    /// <param name="signaturesContent">UTF-8 bytes of the <c>signatures.tw</c> file to sign.</param>
    /// <param name="privateKey">PKCS#8-encoded ECDSA P-384 private key.</param>
    public static SignatureFile Sign(byte[] signaturesContent, byte[] privateKey)
    {
        ArgumentNullException.ThrowIfNull(signaturesContent);
        ArgumentNullException.ThrowIfNull(privateKey);
        using var ecdsa = ECDsa.Create();
        ecdsa.ImportPkcs8PrivateKey(privateKey, out _);

        var publicKey = ecdsa.ExportSubjectPublicKeyInfo();
        var hash = SHA512.HashData(signaturesContent);
        var signature = ecdsa.SignHash(hash);

        return new SignatureFile
        {
            Algorithm = AlgorithmName,
            KeyId = ComputeKeyId(publicKey),
            Timestamp = DateTimeOffset.UtcNow,
            SignatureBytes = signature,
        };
    }

    /// <summary>
    /// Verifies that the signature in <paramref name="signatureFile"/> is valid
    /// for <paramref name="signaturesContent"/> using <paramref name="publicKey"/>.
    /// Accepts both <see cref="AlgorithmName"/> and
    /// <see cref="LegacyAlgorithmName"/>; all other labels are rejected.
    /// </summary>
    /// <param name="signaturesContent">UTF-8 bytes of the <c>signatures.tw</c> file that was signed.</param>
    /// <param name="signatureFile">Parsed <c>signature.sig</c> containing the ECDSA signature bytes and metadata.</param>
    /// <param name="publicKey">X.509 SubjectPublicKeyInfo-encoded ECDSA P-384 public key to verify against.</param>
    public static bool Verify(
        byte[] signaturesContent, SignatureFile signatureFile, byte[] publicKey)
    {
        ArgumentNullException.ThrowIfNull(signaturesContent);
        ArgumentNullException.ThrowIfNull(signatureFile);
        ArgumentNullException.ThrowIfNull(publicKey);
        if (!signatureFile.Algorithm.Equals(AlgorithmName, StringComparison.OrdinalIgnoreCase)
            && !signatureFile.Algorithm.Equals(LegacyAlgorithmName, StringComparison.OrdinalIgnoreCase))
            return false;

        using var ecdsa = ECDsa.Create();
        ecdsa.ImportSubjectPublicKeyInfo(publicKey, out _);

        var hash = SHA512.HashData(signaturesContent);
        return ecdsa.VerifyHash(hash, signatureFile.SignatureBytes);
    }

    /// <summary>
    /// Verifies the signature against a set of trusted public keys. Returns
    /// <c>true</c> as soon as one key matches. An empty <paramref name="publicKeys"/>
    /// list always returns <c>false</c>.
    /// </summary>
    /// <param name="signaturesContent">UTF-8 bytes of the <c>signatures.tw</c> file that was signed.</param>
    /// <param name="signatureFile">Parsed <c>signature.sig</c> containing the ECDSA signature bytes and metadata.</param>
    /// <param name="publicKeys">Trusted public keys (X.509 SubjectPublicKeyInfo). Verification succeeds if any key matches.</param>
    public static bool Verify(
        byte[] signaturesContent,
        SignatureFile signatureFile,
        IReadOnlyList<byte[]> publicKeys)
    {
        ArgumentNullException.ThrowIfNull(signaturesContent);
        ArgumentNullException.ThrowIfNull(signatureFile);
        ArgumentNullException.ThrowIfNull(publicKeys);

        foreach (var key in publicKeys)
        {
            if (key is null)
                continue;
            if (Verify(signaturesContent, signatureFile, key))
                return true;
        }

        return false;
    }
}
