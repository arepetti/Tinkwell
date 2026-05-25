using Tinkwell.Package;

namespace Tinkwell.Package.Tests;

public class PackageValidatorTests
{
    private readonly PackageValidator _validator = new();

    // --- NormalizeEntryPath ---

    [Theory]
    [InlineData("content\\file.dll", "content/file.dll")]
    [InlineData("/content/file.dll", "content/file.dll")]
    [InlineData("\\content\\file.dll", "content/file.dll")]
    [InlineData("content/file.dll", "content/file.dll")]
    public void NormalizeEntryPath_NormalizesSlashesAndLeadingSlash(
        string raw, string expected)
    {
        Assert.Equal(expected, _validator.NormalizeEntryPath(raw));
    }

    // --- ValidateEntryPath: valid paths ---

    [Theory]
    [InlineData("package.tw")]
    [InlineData("content/file.dll")]
    [InlineData("content/sub/deep/file.txt")]
    [InlineData("security/signatures.tw")]
    [InlineData("content/my-file_v2.dll")]
    public void ValidateEntryPath_ValidPaths_DoesNotThrow(string path)
    {
        _validator.ValidateEntryPath(path, 260);
    }

    // --- ValidateEntryPath: path traversal ---

    [Theory]
    [InlineData("../etc/passwd")]
    [InlineData("content/../../etc/shadow")]
    [InlineData("content/../../../evil.exe")]
    [InlineData("content/sub/../../..")]
    public void ValidateEntryPath_PathTraversal_Throws(string path)
    {
        var ex = Assert.Throws<PackageException>(
            () => _validator.ValidateEntryPath(path, 260));
        Assert.Contains("traversal", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // --- ValidateEntryPath: self-referencing ---

    [Theory]
    [InlineData("content/./file.dll")]
    [InlineData("./package.tw")]
    public void ValidateEntryPath_SelfReference_Throws(string path)
    {
        var ex = Assert.Throws<PackageException>(
            () => _validator.ValidateEntryPath(path, 260));
        Assert.Contains("Self-referencing", ex.Message);
    }

    // --- ValidateEntryPath: reserved names ---

    [Theory]
    [InlineData("content/CON")]
    [InlineData("content/con.txt")]
    [InlineData("content/NUL")]
    [InlineData("content/nul.dll")]
    [InlineData("content/COM1")]
    [InlineData("content/com1.log")]
    [InlineData("content/LPT1")]
    [InlineData("content/PRN")]
    [InlineData("content/AUX")]
    [InlineData("content/sub/CON")]
    public void ValidateEntryPath_ReservedNames_Throws(string path)
    {
        var ex = Assert.Throws<PackageException>(
            () => _validator.ValidateEntryPath(path, 260));
        Assert.Contains("Reserved", ex.Message);
    }

    // --- ValidateEntryPath: forbidden characters ---

    [Theory]
    [InlineData("content/file:stream")]
    [InlineData("content/file\0name")]
    public void ValidateEntryPath_ForbiddenCharacters_Throws(string path)
    {
        Assert.Throws<PackageException>(
            () => _validator.ValidateEntryPath(path, 260));
    }

    // --- ValidateEntryPath: too long ---

    [Fact]
    public void ValidateEntryPath_TooLong_Throws()
    {
        var longPath = "content/" + new string('a', 300) + ".dll";
        var ex = Assert.Throws<PackageException>(
            () => _validator.ValidateEntryPath(longPath, 260));
        Assert.Contains("exceeds maximum length", ex.Message);
    }

    // --- ValidateEntryPath: empty ---

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void ValidateEntryPath_Empty_Throws(string path)
    {
        Assert.Throws<PackageException>(
            () => _validator.ValidateEntryPath(path, 260));
    }

    // --- ValidateEntryPath: absolute path ---

    [Theory]
    [InlineData("C:\\Windows\\System32\\evil.dll")]
    public void ValidateEntryPath_AbsolutePath_Throws(string path)
    {
        Assert.Throws<PackageException>(
            () => _validator.ValidateEntryPath(path, 260));
    }

    // --- ValidateFileSize ---

    [Fact]
    public void ValidateFileSize_WithinLimit_DoesNotThrow()
    {
        _validator.ValidateFileSize("test.dll", 1000, 2000);
    }

    [Fact]
    public void ValidateFileSize_ExceedsLimit_Throws()
    {
        var ex = Assert.Throws<PackageException>(
            () => _validator.ValidateFileSize("test.dll", 3000, 2000));
        Assert.Contains("exceeds maximum", ex.Message);
    }

    // --- ValidateTotalSize ---

    [Fact]
    public void ValidateTotalSize_WithinLimit_DoesNotThrow()
    {
        _validator.ValidateTotalSize(100, 200);
    }

    [Fact]
    public void ValidateTotalSize_ExceedsLimit_Throws()
    {
        Assert.Throws<PackageException>(
            () => _validator.ValidateTotalSize(300, 200));
    }

    // --- AccumulateDecompressedSize (integer overflow guard for extraction) ---

    [Fact]
    public void AccumulateDecompressedSize_WithinMax_DoesNotThrow()
    {
        long total = 0;
        _validator.AccumulateDecompressedSize(ref total, 100, 1_000);
        _validator.AccumulateDecompressedSize(ref total, 200, 1_000);
        Assert.Equal(300, total);
    }

    [Fact]
    public void AccumulateDecompressedSize_WouldOverflowLong_Throws()
    {
        long total = 100;
        var ex = Assert.Throws<PackageException>(
            () => _validator.AccumulateDecompressedSize(
                ref total, long.MaxValue - 50, 256L * 1024 * 1024 * 1024));
        Assert.Contains("exceeds maximum", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // --- ValidateFileCount ---

    [Fact]
    public void ValidateFileCount_WithinLimit_DoesNotThrow()
    {
        _validator.ValidateFileCount(5, 100);
    }

    [Fact]
    public void ValidateFileCount_ExceedsLimit_Throws()
    {
        Assert.Throws<PackageException>(
            () => _validator.ValidateFileCount(101, 100));
    }

    // --- ValidatePackageStructure ---

    [Fact]
    public void ValidatePackageStructure_ValidStructure_DoesNotThrow()
    {
        var entries = new HashSet<string>
        {
            "package.tw",
            "content/file.dll",
            "security/signatures.tw",
            "security/signature.sig",
        };
        _validator.ValidatePackageStructure(entries);
    }

    [Fact]
    public void ValidatePackageStructure_MissingManifest_Throws()
    {
        var entries = new HashSet<string> { "content/file.dll" };
        var ex = Assert.Throws<PackageException>(
            () => _validator.ValidatePackageStructure(entries));
        Assert.Contains("package.tw", ex.Message);
    }

    [Fact]
    public void ValidatePackageStructure_FileOutsideAllowed_Throws()
    {
        var entries = new HashSet<string> { "package.tw", "rogue.dll" };
        var ex = Assert.Throws<PackageException>(
            () => _validator.ValidatePackageStructure(entries));
        Assert.Contains("outside allowed", ex.Message);
    }

    // --- ValidateCompleteness ---

    [Fact]
    public void ValidateCompleteness_Complete_DoesNotThrow()
    {
        var signed = new HashSet<string> { "package.tw", "content/a.dll" };
        var actual = new HashSet<string>
        {
            "package.tw", "content/a.dll",
            "security/signatures.tw", "security/signature.sig"
        };
        _validator.ValidateCompleteness(signed, actual);
    }

    [Fact]
    public void ValidateCompleteness_UndeclaredFile_Throws()
    {
        var signed = new HashSet<string> { "package.tw" };
        var actual = new HashSet<string>
        {
            "package.tw", "content/extra.dll",
            "security/signatures.tw"
        };
        var ex = Assert.Throws<PackageException>(
            () => _validator.ValidateCompleteness(signed, actual));
        Assert.Contains("Undeclared", ex.Message);
    }

    [Fact]
    public void ValidateCompleteness_MissingSignedFile_Throws()
    {
        var signed = new HashSet<string> { "package.tw", "content/missing.dll" };
        var actual = new HashSet<string> { "package.tw", "security/signatures.tw" };
        var ex = Assert.Throws<PackageException>(
            () => _validator.ValidateCompleteness(signed, actual));
        Assert.Contains("not found", ex.Message);
    }

    [Fact]
    public void ValidateCompleteness_SecurityFilesIgnored_DoesNotThrow()
    {
        var signed = new HashSet<string> { "package.tw" };
        var actual = new HashSet<string>
        {
            "package.tw",
            "security/signatures.tw",
            "security/signature.sig"
        };
        _validator.ValidateCompleteness(signed, actual);
    }

    // --- ValidateSignatureOrder ---

    [Fact]
    public void ValidateSignatureOrder_CanonicalOrder_DoesNotThrow()
    {
        _validator.ValidateSignatureOrder(
            ["content/a.dll", "content/b.dll", "package.tw"]);
    }

    [Fact]
    public void ValidateSignatureOrder_NonCanonical_Throws()
    {
        var ex = Assert.Throws<PackageException>(
            () => _validator.ValidateSignatureOrder(
                ["package.tw", "content/a.dll"]));
        Assert.Contains("canonical order", ex.Message);
    }

    [Fact]
    public void ValidateSignatureOrder_Duplicate_Throws()
    {
        var ex = Assert.Throws<PackageException>(
            () => _validator.ValidateSignatureOrder(
                ["content/a.dll", "content/a.dll"]));
        Assert.Contains("canonical order", ex.Message);
    }

    [Fact]
    public void ValidateSignatureOrder_SingleEntry_DoesNotThrow()
    {
        _validator.ValidateSignatureOrder(["package.tw"]);
    }

    [Fact]
    public void ValidateSignatureOrder_Empty_DoesNotThrow()
    {
        _validator.ValidateSignatureOrder([]);
    }
}
