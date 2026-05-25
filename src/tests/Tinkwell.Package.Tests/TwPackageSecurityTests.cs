using System.IO.Compression;
using System.Text;
using Tinkwell.Package;

namespace Tinkwell.Package.Tests;

public class TwPackageSecurityTests : IDisposable
{
    private readonly string _tempDir;
    private readonly TwPackage _packer = new();
    private readonly byte[] _privateKey;
    private readonly byte[] _publicKey;

    public TwPackageSecurityTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "tw-sec-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
        (_privateKey, _publicKey) = PackageSigner.GenerateKeyPair();
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public async Task TamperedContent_HashMismatch()
    {
        var dir = CreateSignedPackage("tamper-content", "original");

        // Tamper with the content file
        var contentFile = Path.Combine(dir, "content", "data.txt");
        File.WriteAllText(contentFile, "TAMPERED");

        var result = await _packer.VerifyAsync(dir,
            new VerifyOptions { TrustedKeys = new[] { _publicKey } });

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues,
            i => i.Code is VerificationCodes.HashMismatch or VerificationCodes.SizeMismatch);
    }

    [Fact]
    public async Task TamperedManifest_HashMismatch()
    {
        var dir = CreateSignedPackage("tamper-manifest", "data");

        // Tamper with package.tw
        File.WriteAllText(Path.Combine(dir, "package.tw"),
            """
            package "evil-redirect" {
              format-version = 1
            }
            """);

        var result = await _packer.VerifyAsync(dir,
            new VerifyOptions { TrustedKeys = new[] { _publicKey } });

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task UndeclaredFile_Detected()
    {
        var dir = CreateSignedPackage("undeclared", "data");

        // Add an extra file
        File.WriteAllText(Path.Combine(dir, "content", "evil.dll"), "injected");

        var result = await _packer.VerifyAsync(dir,
            new VerifyOptions { TrustedKeys = new[] { _publicKey } });

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues,
            i => i.Code == VerificationCodes.UndeclaredFile);
    }

    [Fact]
    public async Task MissingFile_Detected()
    {
        var dir = CreateSignedPackage("missing", "data");

        // Delete the content file
        File.Delete(Path.Combine(dir, "content", "data.txt"));

        var result = await _packer.VerifyAsync(dir,
            new VerifyOptions { TrustedKeys = new[] { _publicKey } });

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues,
            i => i.Code == VerificationCodes.MissingFile);
    }

    [Fact]
    public async Task WrongPublicKey_InvalidSignature()
    {
        var dir = CreateSignedPackage("wrong-key", "data");

        var (_, otherPublicKey) = PackageSigner.GenerateKeyPair();

        var result = await _packer.VerifyAsync(dir,
            new VerifyOptions { TrustedKeys = new[] { otherPublicKey } });

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues,
            i => i.Code == VerificationCodes.InvalidSignature);
    }

    [Fact]
    public async Task TamperedSignaturesTw_InvalidSignature()
    {
        var dir = CreateSignedPackage("tamper-sigtw", "data");

        // Tamper with signatures.tw
        var sigTwPath = Path.Combine(dir, "security", "signatures.tw");
        var content = File.ReadAllText(sigTwPath);
        File.WriteAllText(sigTwPath, content.Replace("sha512", "sha256"));

        var result = await _packer.VerifyAsync(dir,
            new VerifyOptions { TrustedKeys = new[] { _publicKey } });

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues,
            i => i.Code == VerificationCodes.InvalidSignature);
    }

    [Fact]
    public async Task Verify_Default_WithoutTrustedKeys_Throws()
    {
        // Strict-by-default: verification requires either a trust anchor or
        // explicit opt-in to integrity-only. The default VerifyOptions
        // (RequireSignatures=true, no keys, AllowIntegrityOnly=false) must
        // fail fast rather than silently accept.
        var dir = CreateSignedPackage("strict-default", "data");

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _packer.VerifyAsync(dir, new VerifyOptions()));
    }

    [Fact]
    public async Task Verify_AllowIntegrityOnly_EmitsWarning_AndPasses()
    {
        var dir = CreateSignedPackage("integrity-only-ok", "data");

        var result = await _packer.VerifyAsync(dir,
            new VerifyOptions { AllowIntegrityOnly = true });

        Assert.True(result.IsValid);
        Assert.Contains(result.Issues,
            i => i.Severity == VerificationSeverity.Warning
                 && i.Code == VerificationCodes.IntegrityOnly);
    }

    [Fact]
    public async Task Verify_AllowIntegrityOnly_StillDetectsTampering()
    {
        // Integrity-only mode must still catch straight content tampering;
        // what it cannot catch is a forged signatures.tw, exercised below.
        var dir = CreateSignedPackage("integrity-only-tamper", "data");
        File.WriteAllText(Path.Combine(dir, "content", "data.txt"), "TAMPERED");

        var result = await _packer.VerifyAsync(dir,
            new VerifyOptions { AllowIntegrityOnly = true });

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues,
            i => i.Code is VerificationCodes.HashMismatch or VerificationCodes.SizeMismatch);
    }

    [Fact]
    public async Task Verify_ForgedSignaturesTw_AcceptedByIntegrityOnly_BlockedByTrustedKeys()
    {
        // Regression guard for the pre-GA footgun: an attacker who rewrites
        // signatures.tw to match tampered content passes integrity-only
        // verification but must be caught when a trusted key is supplied.
        var dir = CreateSignedPackage("forged-sigtw", "original");

        // Tamper content and rebuild matching signatures.tw (keeping the
        // original signature.sig, which now signs stale content).
        var contentFile = Path.Combine(dir, "content", "data.txt");
        File.WriteAllText(contentFile, "TAMPERED");

        var newHash = PackageHasher.ComputeHash(File.ReadAllBytes(contentFile));
        var manifestBytes = File.ReadAllBytes(Path.Combine(dir, "package.tw"));
        var manifestHash = PackageHasher.ComputeHash(manifestBytes);

        var forged = new PackageSignatures
        {
            Algorithm = PackageHasher.Algorithm,
            Files = new List<FileSignature>
            {
                new() { Path = "content/data.txt", Hash = newHash, Size = new FileInfo(contentFile).Length },
                new() { Path = "package.tw", Hash = manifestHash, Size = manifestBytes.Length },
            },
        };
        File.WriteAllText(
            Path.Combine(dir, "security", "signatures.tw"),
            SignaturesFormat.Write(forged));

        // Integrity-only can be fooled (this is exactly the footgun we warn
        // against), so the trusted-keys path is the real defence.
        var strictResult = await _packer.VerifyAsync(dir,
            new VerifyOptions { TrustedKeys = new[] { _publicKey } });

        Assert.False(strictResult.IsValid);
        Assert.Contains(strictResult.Issues,
            i => i.Code == VerificationCodes.InvalidSignature);
    }

    [Fact]
    public async Task Verify_MultipleTrustedKeys_AcceptsAnyMatch()
    {
        var dir = CreateSignedPackage("multi-keys", "data");

        var (_, otherPublicKey1) = PackageSigner.GenerateKeyPair();
        var (_, otherPublicKey2) = PackageSigner.GenerateKeyPair();

        var result = await _packer.VerifyAsync(dir,
            new VerifyOptions { TrustedKeys = new[] { otherPublicKey1, _publicKey, otherPublicKey2 } });

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Verify_MultipleTrustedKeys_AllMismatch_Fails()
    {
        var dir = CreateSignedPackage("multi-keys-mismatch", "data");

        var (_, otherPublicKey1) = PackageSigner.GenerateKeyPair();
        var (_, otherPublicKey2) = PackageSigner.GenerateKeyPair();

        var result = await _packer.VerifyAsync(dir,
            new VerifyOptions { TrustedKeys = new[] { otherPublicKey1, otherPublicKey2 } });

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues,
            i => i.Code == VerificationCodes.InvalidSignature);
    }

    [Fact]
    [Obsolete("Exercises the obsolete PublicKey shortcut on purpose.")]
    public async Task Verify_ObsoletePublicKeyShortcut_StillWorks()
    {
        var dir = CreateSignedPackage("obsolete-shortcut", "data");

        var result = await _packer.VerifyAsync(dir,
            new VerifyOptions { PublicKey = _publicKey });

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task PathTraversal_ZipEntry_Rejected()
    {
        using var zipStream = new MemoryStream();
        using (var zip = new ZipArchive(zipStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            AddEntry(zip, "package.tw", """package "evil" { format-version = 1 }""");
            AddEntry(zip, "content/legit.txt", "ok");
            AddEntry(zip, "../../../etc/passwd", "evil");
        }

        zipStream.Position = 0;
        var outputDir = Path.Combine(_tempDir, "traversal-output");

        await Assert.ThrowsAsync<PackageException>(() =>
            _packer.UnpackAsync(zipStream, outputDir, new UnpackOptions { Verify = false }));
    }

    [Fact]
    public async Task DuplicateEntries_Rejected()
    {
        using var zipStream = new MemoryStream();
        using (var zip = new ZipArchive(zipStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            AddEntry(zip, "package.tw", """package "dup" { format-version = 1 }""");
            AddEntry(zip, "content/file.txt", "first");
            AddEntry(zip, "content/file.txt", "second");
        }

        zipStream.Position = 0;
        var outputDir = Path.Combine(_tempDir, "dup-output");

        await Assert.ThrowsAsync<PackageException>(() =>
            _packer.UnpackAsync(zipStream, outputDir, new UnpackOptions { Verify = false }));
    }

    [Fact]
    public async Task FileCountExceeded_Rejected()
    {
        using var zipStream = new MemoryStream();
        using (var zip = new ZipArchive(zipStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            AddEntry(zip, "package.tw", """package "big" { format-version = 1 }""");
            for (int i=0; i < 10; ++i)
                AddEntry(zip, $"content/file{i}.txt", "data");
        }

        zipStream.Position = 0;
        var outputDir = Path.Combine(_tempDir, "count-output");

        await Assert.ThrowsAsync<PackageException>(() =>
            _packer.UnpackAsync(zipStream, outputDir,
                new UnpackOptions { Verify = false, MaxFileCount = 5 }));
    }

    [Fact]
    public async Task FileSizeExceeded_Rejected()
    {
        using var zipStream = new MemoryStream();
        using (var zip = new ZipArchive(zipStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            AddEntry(zip, "package.tw", """package "large" { format-version = 1 }""");
            AddEntry(zip, "content/big.bin", new string('X', 1000));
        }

        zipStream.Position = 0;
        var outputDir = Path.Combine(_tempDir, "size-output");

        await Assert.ThrowsAsync<PackageException>(() =>
            _packer.UnpackAsync(zipStream, outputDir,
                new UnpackOptions { Verify = false, MaxFileSize = 500 }));
    }

    [Fact]
    public async Task FileOutsideStructure_Rejected()
    {
        var dir = Path.Combine(_tempDir, "bad-structure");
        Directory.CreateDirectory(dir);
        Directory.CreateDirectory(Path.Combine(dir, "content"));

        File.WriteAllText(Path.Combine(dir, "package.tw"),
            """package "bad" { format-version = 1 }""");
        File.WriteAllText(Path.Combine(dir, "content", "ok.txt"), "ok");
        File.WriteAllText(Path.Combine(dir, "rogue.dll"), "evil");

        var packer = new TwPackage();
        await Assert.ThrowsAsync<PackageException>(() =>
            packer.PackAsync(dir, new MemoryStream()));
    }

    private string CreateSignedPackage(string name, string content)
    {
        var sourceDir = Path.Combine(_tempDir, name + "-src");
        Directory.CreateDirectory(sourceDir);
        Directory.CreateDirectory(Path.Combine(sourceDir, "content"));

        File.WriteAllText(Path.Combine(sourceDir, "package.tw"),
            "package \"" + name + "\" {\n  format-version = 1\n}\n");
        File.WriteAllText(Path.Combine(sourceDir, "content", "data.txt"), content);

        using var zipStream = new MemoryStream();
        _packer.PackAsync(sourceDir, zipStream, new PackOptions { PrivateKey = _privateKey })
            .GetAwaiter().GetResult();

        var extractDir = Path.Combine(_tempDir, name);
        zipStream.Position = 0;
        _packer.UnpackAsync(zipStream, extractDir,
            new UnpackOptions { TrustedKeys = new[] { _publicKey } })
            .GetAwaiter().GetResult();

        return extractDir;
    }

    private static void AddEntry(ZipArchive zip, string path, string content)
    {
        var entry = zip.CreateEntry(path);
        using var stream = entry.Open();
        stream.Write(Encoding.UTF8.GetBytes(content));
    }
}
