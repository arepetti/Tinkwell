using System.Text.RegularExpressions;
using Tinkwell.Bootstrapper.Expressions;

namespace Tinkwell.Bootstrapper.Tests.Expressions;

public class TextHelpersTests
{
    [Theory]
    [InlineData("blueish", "blue", false)]
    [InlineData("blue", "blueish", false)]
    [InlineData("blue", "green", false)]
    [InlineData("", "blue", false)]
    [InlineData("", "", true)]
    [InlineData("blue", "blue", true)]
    [InlineData("blue", "blu?", true)]
    [InlineData("blu", "blu?", false)]
    [InlineData("123", "1?3", true)]
    [InlineData("blue", "*", true)]
    [InlineData("blue", "bl*", true)]
    [InlineData("blue", "*ue", true)]
    [InlineData("blue", "b*e", true)]
    [InlineData("blue", "[Bb]lue", true)]
    [InlineData("Blue", "[Bb]lue", true)]
    [InlineData("Glue", "[Bb]lue", false)]
    [InlineData("Glue", "[^G]lue", false)]
    [InlineData("[value]", @"\[value\]", true)]
    [InlineData(@"\path", @"\\path", true)]
    public void GitLikeWildcardToRegex_MatchesWildcards(string text, string pattern, bool result)
    {
        var regex = new Regex(TextHelpers.GitLikeWildcardToRegex(pattern), RegexOptions.CultureInvariant);
        Assert.Equal(result, regex.IsMatch(text));
    }

    [Theory]
    [InlineData("blue", "green", false)]
    [InlineData("blue", "blue", true)]
    [InlineData("blue", "blu?", true)]
    public void PatternToRegex_MatchesWildcards(string text, string pattern, bool result)
    {
        var regex = TextHelpers.PatternToRegex(pattern);
        Assert.Equal(result, regex.IsMatch(text));
    }

    [Fact]
    public void PatternToRegex_ThrowsForInvalidPattern()
    {
        Assert.Throws<ArgumentException>(() => TextHelpers.PatternToRegex("[[invalid"));
    }
}
