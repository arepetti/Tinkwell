using Tinkwell.Package;

namespace Tinkwell.Package.Tests;

public class SignaturesFormatTests
{
    [Fact]
    public void Parse_ValidSignatures_AllFieldsParsed()
    {
        const string text = """
            signatures sha512 {
              file "content/a.dll" {
                hash = "abcdef1234567890"
                size = 1024
              }
              file "package.tw" {
                hash = "1234567890abcdef"
                size = 256
              }
            }
            """;

        var sigs = SignaturesFormat.Parse(text);

        Assert.Equal("sha512", sigs.Algorithm);
        Assert.Equal(2, sigs.Files.Count);
        Assert.Equal("content/a.dll", sigs.Files[0].Path);
        Assert.Equal("abcdef1234567890", sigs.Files[0].Hash);
        Assert.Equal(1024, sigs.Files[0].Size);
        Assert.Equal("package.tw", sigs.Files[1].Path);
        Assert.Equal("1234567890abcdef", sigs.Files[1].Hash);
        Assert.Equal(256, sigs.Files[1].Size);
    }

    [Fact]
    public void Parse_MissingHash_Throws()
    {
        const string text = """
            signatures sha512 {
              file "test.dll" {
                size = 100
              }
            }
            """;

        Assert.Throws<PackageException>(() => SignaturesFormat.Parse(text));
    }

    [Fact]
    public void Parse_MissingSize_Throws()
    {
        const string text = """
            signatures sha512 {
              file "test.dll" {
                hash = "abc"
              }
            }
            """;

        Assert.Throws<PackageException>(() => SignaturesFormat.Parse(text));
    }

    [Fact]
    public void RoundTrip_PreservesContent()
    {
        var original = new PackageSignatures
        {
            Algorithm = "sha512",
            Files =
            [
                new FileSignature { Path = "content/x.dll", Hash = "aabbcc", Size = 500 },
                new FileSignature { Path = "package.tw", Hash = "ddeeff", Size = 200 },
            ],
        };

        var text = SignaturesFormat.Write(original);
        var parsed = SignaturesFormat.Parse(text);

        Assert.Equal("sha512", parsed.Algorithm);
        Assert.Equal(2, parsed.Files.Count);
        Assert.Equal("content/x.dll", parsed.Files[0].Path);
        Assert.Equal("aabbcc", parsed.Files[0].Hash);
        Assert.Equal(500, parsed.Files[0].Size);
    }
}
