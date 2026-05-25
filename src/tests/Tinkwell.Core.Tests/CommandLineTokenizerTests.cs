using Tinkwell.Text;

namespace Tinkwell.Core.Tests;

public class CommandLineTokenizerTests
{
    [Fact]
    public void SimpleWords_SplitOnWhitespace()
    {
        var tokens = CommandLineTokenizer.Tokenize("notify ready abc123");
        Assert.Equal(["notify", "ready", "abc123"], tokens);
    }

    [Fact]
    public void DoubleQuotedString_KeptAsSingleToken()
    {
        var tokens = CommandLineTokenizer.Tokenize("notify fatal abc \"runlet mismatch\"");
        Assert.Equal(["notify", "fatal", "abc", "runlet mismatch"], tokens);
    }

    [Fact]
    public void SingleQuotedString_KeptAsSingleToken()
    {
        var tokens = CommandLineTokenizer.Tokenize("notify fatal abc 'runlet mismatch'");
        Assert.Equal(["notify", "fatal", "abc", "runlet mismatch"], tokens);
    }

    [Fact]
    public void ExtraWhitespace_IsIgnored()
    {
        var tokens = CommandLineTokenizer.Tokenize("  notify   ready   abc  ");
        Assert.Equal(["notify", "ready", "abc"], tokens);
    }

    [Fact]
    public void EmptyString_ReturnsEmptyArray()
    {
        var tokens = CommandLineTokenizer.Tokenize("");
        Assert.Empty(tokens);
    }

    [Fact]
    public void WhitespaceOnly_ReturnsEmptyArray()
    {
        var tokens = CommandLineTokenizer.Tokenize("   ");
        Assert.Empty(tokens);
    }

    [Fact]
    public void EmptyQuotes_ProducesEmptyToken()
    {
        var tokens = CommandLineTokenizer.Tokenize("notify fatal abc \"\"");
        Assert.Equal(["notify", "fatal", "abc", ""], tokens);
    }

    [Fact]
    public void EscapedDoubleQuote_IsUnescaped()
    {
        var tokens = CommandLineTokenizer.Tokenize("say \"hello \\\"world\\\"\"");
        Assert.Equal(["say", "hello \"world\""], tokens);
    }

    [Fact]
    public void EscapedSingleQuote_IsUnescaped()
    {
        var tokens = CommandLineTokenizer.Tokenize("say 'it\\'s fine'");
        Assert.Equal(["say", "it's fine"], tokens);
    }

    [Fact]
    public void BackslashNotBeforeQuote_IsPreserved()
    {
        var tokens = CommandLineTokenizer.Tokenize("path \"C:\\Users\\test\"");
        Assert.Equal(["path", "C:\\Users\\test"], tokens);
    }

    [Fact]
    public void MixedQuoteStyles_ParsedIndependently()
    {
        var tokens = CommandLineTokenizer.Tokenize("\"double quoted\" 'single quoted' plain");
        Assert.Equal(["double quoted", "single quoted", "plain"], tokens);
    }

    [Fact]
    public void IsBlankOrComment_EmptyLine_ReturnsTrue()
    {
        Assert.True(CommandLineTokenizer.IsBlankOrComment(""));
    }

    [Fact]
    public void IsBlankOrComment_WhitespaceOnly_ReturnsTrue()
    {
        Assert.True(CommandLineTokenizer.IsBlankOrComment("   \t  "));
    }

    [Fact]
    public void IsBlankOrComment_HashComment_ReturnsTrue()
    {
        Assert.True(CommandLineTokenizer.IsBlankOrComment("  # this is a comment"));
    }

    [Fact]
    public void IsBlankOrComment_SlashSlashComment_ReturnsTrue()
    {
        Assert.True(CommandLineTokenizer.IsBlankOrComment("  // also a comment"));
    }

    [Fact]
    public void IsBlankOrComment_RealCommand_ReturnsFalse()
    {
        Assert.False(CommandLineTokenizer.IsBlankOrComment("measures list"));
    }

    [Fact]
    public void IsBlankOrComment_SingleSlash_ReturnsFalse()
    {
        Assert.False(CommandLineTokenizer.IsBlankOrComment("/ not a comment"));
    }
}
