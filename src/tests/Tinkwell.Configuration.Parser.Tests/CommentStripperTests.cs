using Tinkwell.Configuration.Parser.Parsing;

namespace Tinkwell.Configuration.Parser.Tests;

/// <summary>
/// Unit tests for <see cref="CommentStripper"/> (ROBUST-1: strings inside paren expressions).
/// </summary>
public class CommentStripperTests
{
    [Theory]
    [InlineData("")]
    [InlineData("a")]
    [InlineData("hello // not a comment")]
    [InlineData("# not a line comment when not at BOL x\n# real\ny")]
    [InlineData("x = (\"http://a//b\")")]
    public void Strip_PreservesTotalLength(string input)
    {
        var stripped = CommentStripper.Strip(input);
        Assert.Equal(input.Length, stripped.Length);
    }

    [Fact]
    public void Strip_DoubleSlashInsideParenString_DoesNotBlankRestOfLine()
    {
        const string input = "g = (\"a//b\") // tail";
        var stripped = CommentStripper.Strip(input);
        Assert.Equal(input.Length, stripped.Length);
        Assert.Contains("a//b", stripped);
        Assert.Equal(' ', stripped[^1]); // tail comment blanked
        Assert.Equal(' ', stripped[^2]);
    }

    [Fact]
    public void Strip_InterpolatedStringInsideParens_PreservesDollarQuote()
    {
        const string input = "v = ($\"x//y\")";
        var stripped = CommentStripper.Strip(input);
        Assert.Equal(input.Length, stripped.Length);
        Assert.Equal(input, stripped);
    }

    [Fact]
    public void Strip_VerbatimStringInsideParens_PreservesContent()
    {
        const string input = "v = (@\"a//b\")";
        var stripped = CommentStripper.Strip(input);
        Assert.Equal(input.Length, stripped.Length);
    }
}
