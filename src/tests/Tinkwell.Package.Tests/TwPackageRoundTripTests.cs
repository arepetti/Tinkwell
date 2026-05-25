using System.IO.Compression;
using System.Text;
using Tinkwell.Package;

namespace Tinkwell.Package.Tests;

public class TwPackageRoundTripTests : IDisposable
{
    private readonly string _tempDir;
    private readonly TwPackage _packer = new();

    public TwPackageRoundTripTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "tw-test-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public async Task PackAndUnpack_Signed_RoundTrips()
    {
        var (privateKey, publicKey) = PackageSigner.GenerateKeyPair();
        var sourceDir = CreateSamplePackageDir("signed-test", "Hello World");

        using var zipStream = new MemoryStream();
        await _packer.PackAsync(sourceDir, zipStream, new PackOptions { PrivateKey = privateKey });

        zipStream.Position = 0;
        var outputDir = Path.Combine(_tempDir, "output");
        await _packer.UnpackAsync(zipStream, outputDir,
            new UnpackOptions { TrustedKeys = new[] { publicKey } });

        Assert.True(File.Exists(Path.Combine(outputDir, "package.tw")));
        Assert.True(File.Exists(Path.Combine(outputDir, "content", "data.txt")));
        Assert.Equal("Hello World",
            File.ReadAllText(Path.Combine(outputDir, "content", "data.txt")));
    }

    [Fact]
    public async Task PackAndUnpack_Unsigned_RoundTrips()
    {
        var sourceDir = CreateSamplePackageDir("unsigned-test", "Content");

        using var zipStream = new MemoryStream();
        await _packer.PackAsync(sourceDir, zipStream,
            new PackOptions { Sign = false });

        zipStream.Position = 0;
        var outputDir = Path.Combine(_tempDir, "output-unsigned");
        await _packer.UnpackAsync(zipStream, outputDir,
            new UnpackOptions { Verify = false });

        Assert.True(File.Exists(Path.Combine(outputDir, "package.tw")));
        Assert.Equal("Content",
            File.ReadAllText(Path.Combine(outputDir, "content", "data.txt")));
    }

    [Fact]
    public async Task PackFromManifest_AndVerify_Succeeds()
    {
        var (privateKey, publicKey) = PackageSigner.GenerateKeyPair();

        var manifest = new PackageManifest
        {
            Name = "programmatic",
            Version = "1.0.0",
            Author = "Test",
        };

        var entries = new[]
        {
            new PackageEntry("lib.dll", new MemoryStream("fake dll content"u8.ToArray())),
        };

        using var zipStream = new MemoryStream();
        await _packer.PackAsync(manifest, entries, zipStream,
            new PackOptions { PrivateKey = privateKey });

        // Save to file for verify
        var zipPath = Path.Combine(_tempDir, "programmatic.zip");
        File.WriteAllBytes(zipPath, zipStream.ToArray());

        var result = await _packer.VerifyAsync(zipPath,
            new VerifyOptions { TrustedKeys = new[] { publicKey } });

        Assert.True(result.IsValid);
        Assert.Empty(result.Issues.Where(i => i.Severity == VerificationSeverity.Error));
    }

    [Fact]
    public async Task Verify_DirectoryPackage_Succeeds()
    {
        var (privateKey, publicKey) = PackageSigner.GenerateKeyPair();
        var sourceDir = CreateSamplePackageDir("dir-verify", "test data");

        using var zipStream = new MemoryStream();
        await _packer.PackAsync(sourceDir, zipStream, new PackOptions { PrivateKey = privateKey });

        zipStream.Position = 0;
        var extractDir = Path.Combine(_tempDir, "extracted");
        await _packer.UnpackAsync(zipStream, extractDir,
            new UnpackOptions { TrustedKeys = new[] { publicKey } });

        var result = await _packer.VerifyAsync(extractDir,
            new VerifyOptions { TrustedKeys = new[] { publicKey } });

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Verify_ViaManifestPath_Succeeds()
    {
        var (privateKey, publicKey) = PackageSigner.GenerateKeyPair();
        var sourceDir = CreateSamplePackageDir("manifest-path", "test");

        using var zipStream = new MemoryStream();
        await _packer.PackAsync(sourceDir, zipStream, new PackOptions { PrivateKey = privateKey });

        zipStream.Position = 0;
        var extractDir = Path.Combine(_tempDir, "extracted2");
        await _packer.UnpackAsync(zipStream, extractDir,
            new UnpackOptions { TrustedKeys = new[] { publicKey } });

        var manifestPath = Path.Combine(extractDir, "package.tw");
        var result = await _packer.VerifyAsync(manifestPath,
            new VerifyOptions { TrustedKeys = new[] { publicKey } });

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Resign_ProducesValidPackage()
    {
        var (key1Private, _) = PackageSigner.GenerateKeyPair();
        var (key2Private, key2Public) = PackageSigner.GenerateKeyPair();

        var sourceDir = CreateSamplePackageDir("resign-test", "resign data");

        using var original = new MemoryStream();
        await _packer.PackAsync(sourceDir, original, new PackOptions { PrivateKey = key1Private });

        original.Position = 0;
        using var resigned = new MemoryStream();
        await _packer.ResignAsync(original, resigned, key2Private);

        var zipPath = Path.Combine(_tempDir, "resigned.zip");
        File.WriteAllBytes(zipPath, resigned.ToArray());

        var result = await _packer.VerifyAsync(zipPath,
            new VerifyOptions { TrustedKeys = new[] { key2Public } });

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Verify_NoSignatures_RequiredByDefault_Fails()
    {
        // An unsigned package with default (strict) verification. The fail-fast
        // guard rejects the call because it's ambiguous whether the caller
        // expects signatures to be there; callers must opt in to one of:
        //   - TrustedKeys + RequireSignatures=true
        //   - AllowIntegrityOnly=true
        //   - RequireSignatures=false
        var sourceDir = CreateSamplePackageDir("no-sig", "data");

        using var zipStream = new MemoryStream();
        await _packer.PackAsync(sourceDir, zipStream, new PackOptions { Sign = false });

        var zipPath = Path.Combine(_tempDir, "nosig.zip");
        File.WriteAllBytes(zipPath, zipStream.ToArray());

        await Assert.ThrowsAsync<ArgumentException>(() => _packer.VerifyAsync(zipPath));
    }

    [Fact]
    public async Task Verify_NoSignatures_NotRequired_Succeeds()
    {
        var sourceDir = CreateSamplePackageDir("no-sig-ok", "data");

        using var zipStream = new MemoryStream();
        await _packer.PackAsync(sourceDir, zipStream, new PackOptions { Sign = false });

        var zipPath = Path.Combine(_tempDir, "nosig-ok.zip");
        File.WriteAllBytes(zipPath, zipStream.ToArray());

        var result = await _packer.VerifyAsync(zipPath,
            new VerifyOptions { RequireSignatures = false });

        Assert.True(result.IsValid);
        Assert.Contains(result.Issues,
            i => i.Severity == VerificationSeverity.Warning
                 && i.Code == VerificationCodes.Unsigned);
    }

    private string CreateSamplePackageDir(string name, string contentData)
    {
        var dir = Path.Combine(_tempDir, name);
        Directory.CreateDirectory(dir);
        Directory.CreateDirectory(Path.Combine(dir, "content"));

        File.WriteAllText(Path.Combine(dir, "package.tw"),
            "package \"" + name + "\" {\n  format-version = 1\n  version = \"1.0.0\"\n}\n");

        File.WriteAllText(Path.Combine(dir, "content", "data.txt"), contentData);
        return dir;
    }
}
