using System.Text;
using Tinkwell.Package;

namespace Tinkwell.Package.Tests;

public class PackageSignerTests
{
    [Fact]
    public void GenerateKeyPair_ProducesNonEmptyKeys()
    {
        var (privateKey, publicKey) = PackageSigner.GenerateKeyPair();
        Assert.NotEmpty(privateKey);
        Assert.NotEmpty(publicKey);
    }

    [Fact]
    public void Sign_Verify_RoundTrip()
    {
        var (privateKey, publicKey) = PackageSigner.GenerateKeyPair();
        var data = Encoding.UTF8.GetBytes("test data for signing");

        var sig = PackageSigner.Sign(data, privateKey);

        Assert.Equal(PackageSigner.AlgorithmName, sig.Algorithm);
        Assert.NotEmpty(sig.SignatureBytes);
        Assert.NotEmpty(sig.KeyId);
        Assert.True(sig.Timestamp > DateTimeOffset.MinValue);

        Assert.True(PackageSigner.Verify(data, sig, publicKey));
    }

    [Fact]
    public void Sign_UsesCanonicalAlgorithmLabel()
    {
        // Regression guard: the label written into new signature files must
        // match what Verify expects by default. Early pre-GA builds wrote the
        // legacy 'ecdsa-p384-sha384' label while hashing with SHA-512 -- a
        // mismatch that would silently desync keys and files.
        var (privateKey, _) = PackageSigner.GenerateKeyPair();
        var sig = PackageSigner.Sign(Encoding.UTF8.GetBytes("whatever"), privateKey);

        Assert.Equal("ecdsa-p384-sha512", PackageSigner.AlgorithmName);
        Assert.Equal(PackageSigner.AlgorithmName, sig.Algorithm);
    }

    [Fact]
    public void Verify_AcceptsLegacyAlgorithmLabel()
    {
        // Back-compat: signatures produced by early pre-GA builds carry the
        // 'ecdsa-p384-sha384' label but the bytes were hashed with SHA-512.
        // Verify must continue to accept them.
        var (privateKey, publicKey) = PackageSigner.GenerateKeyPair();
        var data = Encoding.UTF8.GetBytes("legacy labelled payload");

        var sig = PackageSigner.Sign(data, privateKey);
        var legacy = new SignatureFile
        {
            Algorithm = PackageSigner.LegacyAlgorithmName,
            KeyId = sig.KeyId,
            Timestamp = sig.Timestamp,
            SignatureBytes = sig.SignatureBytes,
        };

        Assert.True(PackageSigner.Verify(data, legacy, publicKey));
    }

    [Fact]
    public void Verify_RejectsUnknownAlgorithmLabel()
    {
        var (privateKey, publicKey) = PackageSigner.GenerateKeyPair();
        var data = Encoding.UTF8.GetBytes("payload");

        var sig = PackageSigner.Sign(data, privateKey);
        var mangled = new SignatureFile
        {
            Algorithm = "rsa-4096-md5",
            KeyId = sig.KeyId,
            Timestamp = sig.Timestamp,
            SignatureBytes = sig.SignatureBytes,
        };

        Assert.False(PackageSigner.Verify(data, mangled, publicKey));
    }

    [Fact]
    public void Verify_MultiKey_MatchesAny()
    {
        var (priv, pub) = PackageSigner.GenerateKeyPair();
        var (_, otherPub1) = PackageSigner.GenerateKeyPair();
        var (_, otherPub2) = PackageSigner.GenerateKeyPair();
        var data = Encoding.UTF8.GetBytes("multi");

        var sig = PackageSigner.Sign(data, priv);

        Assert.True(PackageSigner.Verify(data, sig, new[] { otherPub1, pub, otherPub2 }));
    }

    [Fact]
    public void Verify_MultiKey_AllMismatch()
    {
        var (priv, _) = PackageSigner.GenerateKeyPair();
        var (_, otherPub1) = PackageSigner.GenerateKeyPair();
        var (_, otherPub2) = PackageSigner.GenerateKeyPair();
        var data = Encoding.UTF8.GetBytes("multi");

        var sig = PackageSigner.Sign(data, priv);

        Assert.False(PackageSigner.Verify(data, sig, new[] { otherPub1, otherPub2 }));
    }

    [Fact]
    public void Verify_MultiKey_EmptyListRejects()
    {
        var (priv, _) = PackageSigner.GenerateKeyPair();
        var sig = PackageSigner.Sign(Encoding.UTF8.GetBytes("x"), priv);

        Assert.False(PackageSigner.Verify(
            Encoding.UTF8.GetBytes("x"), sig, Array.Empty<byte[]>()));
    }

    [Fact]
    public void Verify_TamperedData_Fails()
    {
        var (privateKey, publicKey) = PackageSigner.GenerateKeyPair();
        var data = Encoding.UTF8.GetBytes("original data");

        var sig = PackageSigner.Sign(data, privateKey);

        var tampered = Encoding.UTF8.GetBytes("tampered data");
        Assert.False(PackageSigner.Verify(tampered, sig, publicKey));
    }

    [Fact]
    public void Verify_WrongKey_Fails()
    {
        var (privateKey1, _) = PackageSigner.GenerateKeyPair();
        var (_, publicKey2) = PackageSigner.GenerateKeyPair();
        var data = Encoding.UTF8.GetBytes("test data");

        var sig = PackageSigner.Sign(data, privateKey1);

        Assert.False(PackageSigner.Verify(data, sig, publicKey2));
    }

    [Fact]
    public void ComputeKeyId_Deterministic()
    {
        var (_, publicKey) = PackageSigner.GenerateKeyPair();
        var id1 = PackageSigner.ComputeKeyId(publicKey);
        var id2 = PackageSigner.ComputeKeyId(publicKey);

        Assert.Equal(id1, id2);
        Assert.StartsWith("SHA256:", id1);
    }

    [Fact]
    public void SignatureFile_RoundTrip()
    {
        var (privateKey, _) = PackageSigner.GenerateKeyPair();
        var data = Encoding.UTF8.GetBytes("test content");

        var sig = PackageSigner.Sign(data, privateKey);
        var bytes = SignatureFileFormat.Write(sig);
        var parsed = SignatureFileFormat.Parse(bytes);

        Assert.Equal(sig.Algorithm, parsed.Algorithm);
        Assert.Equal(sig.KeyId, parsed.KeyId);
        Assert.Equal(sig.SignatureBytes, parsed.SignatureBytes);
    }

    [Fact]
    public void Parse_OversizedFile_Throws()
    {
        var data = new byte[SignatureFileFormat.MaxFileSize + 1];
        var ex = Assert.Throws<PackageException>(() => SignatureFileFormat.Parse(data));
        Assert.Contains("exceeds maximum size", ex.Message);
    }

    [Fact]
    public void Parse_OversizedHeader_Throws()
    {
        var header = "algorithm: test\nkey-id: abc\n" + new string('x', SignatureFileFormat.MaxHeaderSize) + "\n";
        var headerBytes = Encoding.UTF8.GetBytes(header);
        var separator = "---\n"u8.ToArray();
        var sigBytes = new byte[10];

        var data = new byte[headerBytes.Length + separator.Length + sigBytes.Length];
        Buffer.BlockCopy(headerBytes, 0, data, 0, headerBytes.Length);
        Buffer.BlockCopy(separator, 0, data, headerBytes.Length, separator.Length);
        Buffer.BlockCopy(sigBytes, 0, data, headerBytes.Length + separator.Length, sigBytes.Length);

        var ex = Assert.Throws<PackageException>(() => SignatureFileFormat.Parse(data));
        Assert.Contains("header exceeds maximum", ex.Message);
    }

    [Fact]
    public void Parse_OversizedSignatureBytes_Throws()
    {
        var header = "algorithm: test\nkey-id: abc\ntimestamp: 2025-01-01T00:00:00Z\n";
        var headerBytes = Encoding.UTF8.GetBytes(header);
        var separator = "---\n"u8.ToArray();
        var sigBytes = new byte[SignatureFileFormat.MaxSignatureSize + 1];

        var data = new byte[headerBytes.Length + separator.Length + sigBytes.Length];
        Buffer.BlockCopy(headerBytes, 0, data, 0, headerBytes.Length);
        Buffer.BlockCopy(separator, 0, data, headerBytes.Length, separator.Length);
        Buffer.BlockCopy(sigBytes, 0, data, headerBytes.Length + separator.Length, sigBytes.Length);

        var ex = Assert.Throws<PackageException>(() => SignatureFileFormat.Parse(data));
        Assert.Contains("signature exceeds maximum", ex.Message);
    }

    [Fact]
    public void Parse_OversizedFieldValue_Throws()
    {
        var longValue = new string('a', SignatureFileFormat.MaxFieldLength + 1);
        var header = $"algorithm: {longValue}\nkey-id: abc\n";
        var headerBytes = Encoding.UTF8.GetBytes(header);
        var separator = "---\n"u8.ToArray();
        var sigBytes = new byte[10];

        var data = new byte[headerBytes.Length + separator.Length + sigBytes.Length];
        Buffer.BlockCopy(headerBytes, 0, data, 0, headerBytes.Length);
        Buffer.BlockCopy(separator, 0, data, headerBytes.Length, separator.Length);
        Buffer.BlockCopy(sigBytes, 0, data, headerBytes.Length + separator.Length, sigBytes.Length);

        var ex = Assert.Throws<PackageException>(() => SignatureFileFormat.Parse(data));
        Assert.Contains("exceeds maximum length", ex.Message);
    }
}
