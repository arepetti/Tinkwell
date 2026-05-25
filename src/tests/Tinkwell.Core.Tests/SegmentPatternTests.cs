using Tinkwell.Text;

namespace Tinkwell.Core.Tests;

public class SegmentPatternTests
{
    [Theory]
    [InlineData("a-b", "a-b", true)]
    [InlineData("a-*-c", "a-x-c", true)]
    [InlineData("a-*-c", "a-x-y-c", false)]
    [InlineData("a|b", "a", true)]
    [InlineData("a|b", "b", true)]
    [InlineData("a|b", "c", false)]
    [InlineData("x86_64-pc-linux-gnu", "x86_64-pc-linux-gnu", true)]
    public void IsMatch_MatchesSegmentWildcardsAndAlternation(string pattern, string text, bool expected) =>
        Assert.Equal(expected, SegmentPattern.IsMatch(pattern, text));

    [Fact]
    public void ToRegex_SamePattern_ReturnsCachedInstance()
    {
        const string p = "foo-*-bar";
        var a = SegmentPattern.ToRegex(p);
        var b = SegmentPattern.ToRegex(p);
        Assert.Same(a, b);
    }
}
