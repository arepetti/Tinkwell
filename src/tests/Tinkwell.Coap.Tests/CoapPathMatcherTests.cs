using Tinkwell.Coap;

namespace Tinkwell.Coap.Tests;

public class CoapPathMatcherTests
{
    [Theory]
    [InlineData("/a/b/c", "/a/b/c", true)]
    [InlineData("/a/b/c", "/a/b/d", false)]
    [InlineData("/a/b", "/a/b/c", false)]
    [InlineData("/a/b/c", "/a/b", false)]
    public void ExactMatch(string pattern, string path, bool expected)
    {
        Assert.Equal(expected, CoapPathMatcher.IsMatch(pattern, path));
    }

    [Theory]
    [InlineData("/+/b", "/a/b", true)]
    [InlineData("/a/+", "/a/b", true)]
    [InlineData("/+/+", "/a/b", true)]
    [InlineData("/+/b", "/a/c", false)]
    [InlineData("/+", "/a", true)]
    [InlineData("/+", "/a/b", false)]
    public void SingleWildcard(string pattern, string path, bool expected)
    {
        Assert.Equal(expected, CoapPathMatcher.IsMatch(pattern, path));
    }

    [Theory]
    [InlineData("/#", "/a", true)]
    [InlineData("/#", "/a/b/c/d", true)]
    [InlineData("/a/#", "/a", true)]
    [InlineData("/a/#", "/a/b", true)]
    [InlineData("/a/#", "/a/b/c/d/e", true)]
    [InlineData("/a/#", "/b", false)]
    public void MultiWildcard(string pattern, string path, bool expected)
    {
        Assert.Equal(expected, CoapPathMatcher.IsMatch(pattern, path));
    }

    [Fact]
    public void CaseSensitive()
    {
        Assert.False(CoapPathMatcher.IsMatch("/Sensors/Temp", "/sensors/temp"));
    }

    [Fact]
    public void LeadingTrailingSlashesIgnored()
    {
        Assert.True(CoapPathMatcher.IsMatch("sensors/temp", "/sensors/temp/"));
    }

    [Fact]
    public void EmptySegmentsCollapsed()
    {
        Assert.True(CoapPathMatcher.IsMatch("/a//b", "/a/b"));
    }

    [Fact]
    public void IsMatch_NullPattern_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => CoapPathMatcher.IsMatch(null!, "/a"));
    }

    [Fact]
    public void IsMatch_NullPath_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => CoapPathMatcher.IsMatch("/a", null!));
    }

    [Fact]
    public void EmptyPattern_MatchesOnlyEmptyPath()
    {
        Assert.True(CoapPathMatcher.IsMatch("", "/"));
        Assert.False(CoapPathMatcher.IsMatch("", "/a"));
    }

    [Theory]
    [InlineData("/a/", "/a")]
    [InlineData("/a", "/a/")]
    public void TrailingSlash_DoesNotBreakExactMatch(string pattern, string path)
    {
        Assert.True(CoapPathMatcher.IsMatch(pattern, path));
    }

    [Fact]
    public void Pattern_OnlyHash_MatchesAnyNonEmptyPath()
    {
        Assert.True(CoapPathMatcher.IsMatch("#", "/x"));
        Assert.True(CoapPathMatcher.IsMatch("#", "/a/b"));
    }

    [Fact]
    public void Pattern_OnlyPlus_MatchesSingleSegmentPath()
    {
        Assert.True(CoapPathMatcher.IsMatch("+", "/a"));
        Assert.False(CoapPathMatcher.IsMatch("+", "/a/b"));
    }

    [Fact]
    public void Pattern_PlusAtRoot_DoesNotMatchNestedPath()
    {
        Assert.True(CoapPathMatcher.IsMatch("/+", "/a"));
        Assert.False(CoapPathMatcher.IsMatch("/+", "/a/b"));
    }

    [Fact]
    public void EmptyPath_MatchesEmptyPattern_ButNotSegmentedPattern()
    {
        Assert.True(CoapPathMatcher.IsMatch("", ""));
        Assert.False(CoapPathMatcher.IsMatch("/a", ""));
    }

    [Fact]
    public void HashPattern_MatchesEmptyPathSegments()
    {
        Assert.True(CoapPathMatcher.IsMatch("#", ""));
    }

    [Fact]
    public void RootOnlyPattern_MatchesUriPathRootSemantics()
    {
        Assert.True(CoapPathMatcher.IsMatch("/", "/"));
        Assert.False(CoapPathMatcher.IsMatch("/", "/a"));
    }
}
