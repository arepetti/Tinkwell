using System.Text;
using Tinkwell.Package;

namespace Tinkwell.Package.Tests;

public class TwReaderWriterTests
{
    [Fact]
    public void ReadSingleBlock_UnexpectedTokenAfterIdentifier_ThrowsPackageException()
    {
        // Regression: "foo @bar" must not spin — parser advances or throws.
        const string text = """
            package "x" {
              foo @bar
            }
            """;

        var ex = Assert.Throws<PackageException>(() => ManifestFormat.Parse(text));
        Assert.Contains("Unexpected content", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ReadSingleBlock_SignaturesUnexpectedToken_ThrowsPackageException()
    {
        const string text = """
            signatures sha512 {
              junk @here
              file "a" {
                hash = "aabbcc"
                size = 1
              }
            }
            """;

        var ex = Assert.Throws<PackageException>(() => SignaturesFormat.Parse(text));
        Assert.Contains("Unexpected content", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TwWriter_NumericValues_UsesInvariantCultureInSignaturesBranch()
    {
        var block = new TwBlock("signatures", "sha512", new Dictionary<string, string>(),
        [
            new TwBlock("file", "content/x", new Dictionary<string, string>
            {
                ["hash"] = "aabbcc",
                // Large enough to be culture-sensitive in some locales if not Invariant
                ["size"] = 12345.ToString(System.Globalization.CultureInfo.InvariantCulture),
            }, []),
        ]);

        var written = TwWriter.Write(block);
        var parsed = TwReader.ReadSingleBlock(written);
        var child = parsed.Children[0];
        Assert.Equal(12345, long.Parse(
            child.Properties["size"]!,
            System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public void TwWriter_QuoteIfNeeded_SpecialName_RoundTripsThroughReader()
    {
        var block = new TwBlock("package", "a b", new Dictionary<string, string>
        {
            ["format-version"] = "1",
        }, []);
        var text = TwWriter.Write(block);
        var round = TwReader.ReadSingleBlock(text);
        Assert.Equal("a b", round.Name);
        Assert.Equal("1", round.Properties["format-version"]);
    }
}
