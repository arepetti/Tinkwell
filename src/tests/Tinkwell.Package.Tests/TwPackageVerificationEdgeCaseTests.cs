using System.IO.Compression;
using System.Text;
using Tinkwell.Package;

namespace Tinkwell.Package.Tests;

public class TwPackageVerificationEdgeCaseTests : IDisposable
{
    private static readonly Encoding s_utf8NoBom = new UTF8Encoding(false);

    private readonly string _tempDir;
    private readonly TwPackage _packer = new();
    private readonly byte[] _privateKey;
    private readonly byte[] _publicKey;

    public TwPackageVerificationEdgeCaseTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "tw-edge-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
        (_privateKey, _publicKey) = PackageSigner.GenerateKeyPair();
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public async Task VerifyAsync_StrictLayout_ExtraTopLevelFile_ReturnsInvalidStructure()
    {
        var dir = CreateSignedPackage("strict-layout", "x");
        File.WriteAllText(Path.Combine(dir, "readme.txt"), "extra");

        var result = await _packer.VerifyAsync(dir, new VerifyOptions
        {
            TrustedKeys = new[] { _publicKey },
            StrictLayout = true,
        });

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues,
            i => i.Severity == VerificationSeverity.Error
                 && i.Code == VerificationCodes.InvalidStructure);
    }

    [Fact]
    public async Task UnpackAsync_StrictLayout_ExtraFile_ThrowsPackageException()
    {
        var dir = CreateSignedPackage("strict-unpack", "x");
        var zipPath = Path.Combine(_tempDir, "strict.zip");
        using (var fs = File.Create(zipPath))
        using (var zip = new ZipArchive(fs, ZipArchiveMode.Create, leaveOpen: false))
        {
            foreach (var file in Directory.GetFiles(dir, "*", SearchOption.AllDirectories))
            {
                var rel = Path.GetRelativePath(dir, file).Replace('\\', '/');
                var e = zip.CreateEntry(rel, CompressionLevel.Optimal);
                await using (var s = e.Open())
                await using (var input = File.OpenRead(file))
                    await input.CopyToAsync(s);
            }

            var extra = zip.CreateEntry("rogue.txt", CompressionLevel.Optimal);
            await using (var s = extra.Open())
                s.Write(Encoding.UTF8.GetBytes("x"));
        }

        await Assert.ThrowsAsync<PackageException>(() =>
            _packer.UnpackAsync(zipPath, Path.Combine(_tempDir, "out"),
                new UnpackOptions
                {
                    TrustedKeys = new[] { _publicKey },
                    StrictLayout = true,
                }));
    }

    [Fact]
    public async Task VerifyAsync_UnknownAlgorithmLabel_EmitsWarning_AndSucceeds()
    {
        var dir = CreateSignedPackage("unknown-alg", "payload");
        var sigTw = Path.Combine(dir, "security", "signatures.tw");
        var raw = File.ReadAllText(sigTw);
        var relabelled = raw.Replace("signatures sha512", "signatures sha256", StringComparison.Ordinal);
        File.WriteAllText(sigTw, relabelled, s_utf8NoBom);
        var newSigBytes = File.ReadAllBytes(sigTw);
        var signed = PackageSigner.Sign(newSigBytes, _privateKey);
        File.WriteAllBytes(
            Path.Combine(dir, "security", "signature.sig"),
            SignatureFileFormat.Write(signed));

        var result = await _packer.VerifyAsync(dir, new VerifyOptions
        {
            TrustedKeys = new[] { _publicKey },
        });

        Assert.True(result.IsValid);
        Assert.Contains(result.Issues,
            w => w.Severity == VerificationSeverity.Warning
                 && w.Code == VerificationCodes.UnknownAlgorithm);
    }

    [Fact]
    public async Task VerifyAsync_ManifestExceedsMaxSize_ReturnsError()
    {
        var name = "big-manifest";
        var dir = Path.Combine(_tempDir, name);
        Directory.CreateDirectory(Path.Combine(dir, "content"));
        var manifest = new byte[TwPackage.MaxManifestFileSizeBytes + 1];
        manifest[0] = (byte)'#';
        File.WriteAllBytes(Path.Combine(dir, "package.tw"), manifest);
        File.WriteAllText(Path.Combine(dir, "content", "a.txt"), "x");

        var result = await _packer.VerifyAsync(dir, new VerifyOptions
        {
            RequireSignatures = false,
        });

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues,
            i => i.Code == VerificationCodes.ParseError
                 && i.Message.Contains("exceeds maximum", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task VerifyAsync_SignaturesFileExceedsMaxSize_ReturnsError()
    {
        var dir = CreateSignedPackage("big-sig", "y");
        var path = Path.Combine(dir, "security", "signatures.tw");
        File.WriteAllBytes(path, new byte[TwPackage.MaxSignaturesFileSizeBytes + 1]);
        // Keep signature file present so the flow reaches size check on .tw
        var result = await _packer.VerifyAsync(dir, new VerifyOptions
        {
            TrustedKeys = new[] { _publicKey },
        });

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues,
            i => i.Code == VerificationCodes.ParseError
                 && i.Message.Contains(WellKnownPaths.Signatures, StringComparison.Ordinal)
                 && i.Message.Contains("exceeds maximum", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task PackAsync_Directory_ManifestExceedsMax_Throws()
    {
        var root = Path.Combine(_tempDir, "pack-huge");
        Directory.CreateDirectory(Path.Combine(root, "content"));
        File.WriteAllBytes(
            Path.Combine(root, "package.tw"),
            new byte[TwPackage.MaxManifestFileSizeBytes + 1]);
        File.WriteAllText(Path.Combine(root, "content", "a.txt"), "a");

        await Assert.ThrowsAsync<PackageException>(
            () => _packer.PackAsync(root, new MemoryStream()));
    }

    [Fact]
    public async Task ResignAsync_ManifestEntryTooLarge_Throws()
    {
        using var input = new MemoryStream();
        using (var zip = new ZipArchive(input, ZipArchiveMode.Create, true))
        {
            var e = zip.CreateEntry(WellKnownPaths.Manifest);
            var big = new byte[TwPackage.MaxManifestFileSizeBytes + 1];
            using var s = e.Open();
            s.Write(big);
        }

        input.Position = 0;
        using var output = new MemoryStream();

        var ex = await Assert.ThrowsAsync<PackageException>(
            () => _packer.ResignAsync(input, output, _privateKey));

        Assert.Contains("exceeds maximum", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task VerifyAsync_HashInManifestMalformedLength_HashMismatch()
    {
        var dir = CreateSignedPackage("malformed-hash", "z");
        var path = Path.Combine(dir, "security", "signatures.tw");
        var raw = File.ReadAllText(path, Encoding.UTF8);
        const string marker = "hash = \"";
        var idx = raw.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(idx >= 0);
        var valueStart = idx + marker.Length;
        var valueEnd = raw.IndexOf('\"', valueStart);
        var h = raw.Substring(valueStart, valueEnd - valueStart);
        // Wrong length: VerifyHash must return false without throwing (PackageHasher).
        var patched = raw[..valueStart] + h[..^1] + raw[valueEnd..];
        File.WriteAllText(path, patched, s_utf8NoBom);
        var b = File.ReadAllBytes(path);
        var signed = PackageSigner.Sign(b, _privateKey);
        File.WriteAllBytes(
            Path.Combine(dir, "security", "signature.sig"),
            SignatureFileFormat.Write(signed));

        var result = await _packer.VerifyAsync(dir, new VerifyOptions
        {
            TrustedKeys = new[] { _publicKey },
        });

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, i => i.Code == VerificationCodes.HashMismatch);
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
}
