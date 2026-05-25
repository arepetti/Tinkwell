using Tinkwell.Package;

namespace Tinkwell.Package.Tests;

public class TwPackageNullArgumentTests
{
    private readonly TwPackage _packer = new();
    private readonly byte[] _key;

    public TwPackageNullArgumentTests()
    {
        (_key, _) = PackageSigner.GenerateKeyPair();
    }

    [Fact]
    public async Task PackAsync_StringPath_NullSourceDirectory_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>("sourceDirectory",
            () => _packer.PackAsync((string)null!, "out.zip"));
    }

    [Fact]
    public async Task PackAsync_StringPath_NullOutputPath_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>("outputPath",
            () => _packer.PackAsync("dir", (string)null!));
    }

    [Fact]
    public async Task PackAsync_Stream_NullSourceDirectory_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>("sourceDirectory",
            () => _packer.PackAsync((string)null!, new MemoryStream()));
    }

    [Fact]
    public async Task PackAsync_Stream_NullOutput_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>("output",
            () => _packer.PackAsync(Path.GetTempPath(), (Stream)null!));
    }

    [Fact]
    public async Task PackAsync_ManifestDirectory_NullManifest_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>("manifest",
            () => _packer.PackAsync(
                (PackageManifest)null!, "c:\\tmp", new MemoryStream()));
    }

    [Fact]
    public async Task PackAsync_ManifestStream_NullContentFiles_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>("contentFiles",
            () => _packer.PackAsync(
                new PackageManifest { Name = "n" },
                (IEnumerable<PackageEntry>)null!, new MemoryStream()));
    }

    [Fact]
    public async Task PackAsync_ManifestStringDir_NullContentDirectory_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>("contentDirectory",
            () => _packer.PackAsync(
                new PackageManifest { Name = "n" },
                (string)null!,
                new MemoryStream()));
    }

    [Fact]
    public async Task VerifyAsync_NullPath_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>("path",
            () => _packer.VerifyAsync((string)null!));
    }

    [Fact]
    public async Task UnpackAsync_NullPackagePath_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>("packagePath",
            () => _packer.UnpackAsync((string)null!, "out"));
    }

    [Fact]
    public async Task UnpackAsync_NullOutputDirectory_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>("outputDirectory",
            () => _packer.UnpackAsync("x.zip", (string)null!));
    }

    [Fact]
    public async Task UnpackAsync_Stream_NullStream_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>("package",
            () => _packer.UnpackAsync((Stream)null!, "out"));
    }

    [Fact]
    public async Task UnpackAsync_Stream_NullOutput_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>("outputDirectory",
            () => _packer.UnpackAsync(new MemoryStream(), (string)null!));
    }

    [Fact]
    public async Task ResignAsync_StringPath_NullInput_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>("packagePath",
            () => _packer.ResignAsync((string)null!, "o.zip", _key));
    }

    [Fact]
    public async Task ResignAsync_StringPath_NullOutput_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>("outputPath",
            () => _packer.ResignAsync("in.zip", (string)null!, _key));
    }

    [Fact]
    public async Task ResignAsync_StringPath_NullKey_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>("privateKey",
            () => _packer.ResignAsync("in.zip", "o.zip", (byte[]?)null!));
    }

    [Fact]
    public async Task ResignAsync_Stream_NullPackage_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>("package",
            () => _packer.ResignAsync((Stream)null!, new MemoryStream(), _key));
    }

    [Fact]
    public async Task ResignAsync_Stream_NullOutput_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>("output",
            () => _packer.ResignAsync(new MemoryStream(), (Stream)null!, _key));
    }

    [Fact]
    public async Task ResignAsync_Stream_NullPrivateKey_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>("privateKey",
            () => _packer.ResignAsync(new MemoryStream(), new MemoryStream(), (byte[]?)null!));
    }
}
