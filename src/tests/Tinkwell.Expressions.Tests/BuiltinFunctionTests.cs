using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Tinkwell.Expressions;
using Tinkwell.Expressions.Functions;

namespace Tinkwell.Expressions.Tests;

public class BuiltinFunctionTests
{
    private readonly ExpressionEvaluator _evaluator;

    public BuiltinFunctionTests()
    {
        _evaluator = new ExpressionEvaluator(ExpressionFunctionDiscovery.BuiltIn());
    }

    // --- String functions ---

    [Theory]
    [InlineData("to_upper('hello')", "HELLO")]
    [InlineData("to_lower('HELLO')", "hello")]
    [InlineData("trim('  hi  ')", "hi")]
    [InlineData("length('abc')", 3)]
    [InlineData("concat('foo', 'bar')", "foobar")]
    public async Task StringFunctions_BasicOperations(string expr, object expected)
    {
        var result = await _evaluator.EvaluateAsync(expr);
        Assert.Equal(expected, result);
    }

    [Fact]
    public async Task Contains_CaseInsensitive()
    {
        var result = await _evaluator.EvaluateBooleanAsync("contains('Hello World', 'hello')");
        Assert.True(result);
    }

    [Fact]
    public async Task StartsWith_CaseInsensitive()
    {
        var result = await _evaluator.EvaluateBooleanAsync("starts_with('Hello', 'HE')");
        Assert.True(result);
    }

    [Fact]
    public async Task EndsWith_CaseInsensitive()
    {
        var result = await _evaluator.EvaluateBooleanAsync("ends_with('Hello', 'LO')");
        Assert.True(result);
    }

    [Fact]
    public async Task Replace_ReplacesText()
    {
        var result = await _evaluator.EvaluateStringAsync("replace('foo bar foo', 'foo', 'baz')");
        Assert.Equal("baz bar baz", result);
    }

    [Fact]
    public async Task Substring_ExtractsRange()
    {
        var result = await _evaluator.EvaluateStringAsync("substring('hello world', 6, 5)");
        Assert.Equal("world", result);
    }

    [Fact]
    public async Task Substring_NegativeStart_Throws()
    {
        await Assert.ThrowsAsync<ExpressionEvaluationException>(
            () => _evaluator.EvaluateStringAsync("substring('ab', -1, 1)"));
    }

    [Fact]
    public async Task Segment_IndexOutOfRange_Throws()
    {
        await Assert.ThrowsAsync<ExpressionEvaluationException>(
            () => _evaluator.EvaluateStringAsync("segment('a/b', 10)"));
    }

    [Theory]
    [InlineData("sum([c])", null)]
    [InlineData("avg([c])", null)]
    [InlineData("min([c])", null)]
    [InlineData("max([c])", null)]
    [InlineData("first([c])", null)]
    [InlineData("last([c])", null)]
    [InlineData("at([c], 0)", null)]
    [InlineData("skip([c], 1)", null)]
    [InlineData("take([c], 1)", null)]
    public async Task Collection_NullCollection_ReturnsNull(string expr, object? expected)
    {
        var parameters = new Dictionary<string, object?> { ["c"] = null };
        var result = await _evaluator.EvaluateAsync(expr, parameters);
        Assert.Equal(expected, result);
    }

    [Fact]
    public async Task Collection_Count_NullCollection_ReturnsZero()
    {
        var parameters = new Dictionary<string, object?> { ["c"] = null };
        var result = await _evaluator.EvaluateAsync("count([c])", parameters);
        Assert.Equal(0, Convert.ToInt32(result));
    }

    [Fact]
    public async Task Collection_Sum_Empty_ReturnsZero()
    {
        var parameters = new Dictionary<string, object?> { ["c"] = new List<int>() };
        var result = await _evaluator.EvaluateAsync("sum([c])", parameters);
        Assert.Equal(0.0, Convert.ToDouble(result));
    }

    [Fact]
    public async Task Collection_Avg_Empty_Throws()
    {
        var parameters = new Dictionary<string, object?> { ["c"] = new List<int>() };
        await Assert.ThrowsAsync<ExpressionEvaluationException>(
            () => _evaluator.EvaluateAsync("avg([c])", parameters));
    }

    [Fact]
    public async Task JsonValue_ArrayIndexSegment()
    {
        var result = await _evaluator.EvaluateAsync("""json_value('["a","b"]', '1')""");
        Assert.Equal("b", result);
    }

    [Fact]
    public async Task JsonEncode_String_ProducesJsonStringLiteral()
    {
        var result = await _evaluator.EvaluateStringAsync("""json_encode('hi')""");
        Assert.Equal("\"hi\"", result);
    }

    [Fact]
    public async Task JsonPath_ReturnedElement_IsUsableAfterEvaluateAsync()
    {
        var raw = await _evaluator.EvaluateAsync("""json_path('{"k":"v"}', 'k')""");
        var el = Assert.IsType<JsonElement>(raw);
        Assert.Equal(JsonValueKind.String, el.ValueKind);
        Assert.Equal("v", el.GetString());
    }

    [Fact]
    public async Task RegexMatch_ReturnsBool()
    {
        var parameters = new Dictionary<string, object?> { ["pattern"] = @"^[a-z]+\d+$" };
        var result = await _evaluator.EvaluateBooleanAsync("regex_match('abc123', [pattern])", parameters);
        Assert.True(result);
    }

    [Fact]
    public async Task IsNull_TrueForNull()
    {
        var parameters = new Dictionary<string, object?> { ["x"] = null };
        var result = await _evaluator.EvaluateBooleanAsync("is_null([x])", parameters);
        Assert.True(result);
    }

    [Fact]
    public async Task IsNullOrEmpty_TrueForEmpty()
    {
        var result = await _evaluator.EvaluateBooleanAsync("is_null_or_empty('')");
        Assert.True(result);
    }

    [Fact]
    public async Task HasValue_FalseForWhitespace()
    {
        var result = await _evaluator.EvaluateBooleanAsync("has_value('   ')");
        Assert.False(result);
    }

    [Fact]
    public async Task Split_SplitsString()
    {
        var result = await _evaluator.EvaluateAsync("split('a,b,c', ',')");
        var arr = Assert.IsType<string[]>(result);
        Assert.Equal(["a", "b", "c"], arr);
    }

    [Theory]
    [InlineData("segment('sensor/temperature/value', 0)", "sensor")]
    [InlineData("segment('sensor/temperature/value', 1)", "temperature")]
    [InlineData("segment('sensor/temperature/value', -1)", "value")]
    [InlineData("segment('sensor/temperature/value', -2)", "temperature")]
    [InlineData("segment('a/b', 0)", "a")]
    [InlineData("segment('a/b', -1)", "b")]
    public async Task Segment_ExtractsSlashDelimitedPart(string expr, string expected)
    {
        var result = await _evaluator.EvaluateStringAsync(expr);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("segment_at('a.b.c', '.', 1)", "b")]
    [InlineData("segment_at('a.b.c', '.', -1)", "c")]
    [InlineData("segment_at('x/y/z', '/', -2)", "y")]
    public async Task SegmentAt_SupportsNegativeIndex(string expr, string expected)
    {
        var result = await _evaluator.EvaluateStringAsync(expr);
        Assert.Equal(expected, result);
    }

    // --- Conversion functions ---

    [Fact]
    public async Task CBool_ConvertsStringTrue()
    {
        var result = await _evaluator.EvaluateBooleanAsync("cbool('yes')");
        Assert.True(result);
    }

    [Fact]
    public async Task CBool_ZeroIsFalse()
    {
        var result = await _evaluator.EvaluateBooleanAsync("cbool(0)");
        Assert.False(result);
    }

    [Theory]
    [InlineData('x', true)]
    [InlineData('\0', false)]
    public async Task CBool_Char_NonNulIsTrue(char ch, bool expected)
    {
        var parameters = new Dictionary<string, object?> { ["c"] = ch };
        var result = await _evaluator.EvaluateBooleanAsync("cbool([c])", parameters);
        Assert.Equal(expected, result);
    }

    [Fact]
    public async Task CStr_ConvertsNumber()
    {
        var result = await _evaluator.EvaluateStringAsync("cstr(42)");
        Assert.Equal("42", result);
    }

    // --- Security functions ---

    [Fact]
    public async Task Base64_RoundTrips()
    {
        var result = await _evaluator.EvaluateStringAsync("base64_decode(base64_encode('hello'))");
        Assert.Equal("hello", result);
    }

    [Fact]
    public async Task Sha256_ProducesHex()
    {
        var result = await _evaluator.EvaluateStringAsync("sha256('test')");
        Assert.NotNull(result);
        Assert.Equal(64, result.Length);
    }

    // --- Format functions ---

    [Fact]
    public async Task JsonValue_ExtractsField()
    {
        var result = await _evaluator.EvaluateAsync("json_value('{\"name\":\"alice\"}', 'name')");
        Assert.Equal("alice", result);
    }

    [Fact]
    public async Task MakeJson_CreatesJsonString()
    {
        var result = await _evaluator.EvaluateStringAsync("make_json('key', 'value')");
        Assert.Contains("\"key\"", result);
        Assert.Contains("\"value\"", result);
    }

    // --- DateTime functions ---

    [Fact]
    public async Task Now_ReturnsDateTimeCloseToUtcNow()
    {
        var result = await _evaluator.EvaluateAsync("now()");
        var dt = Assert.IsType<DateTime>(result);
        Assert.True((DateTime.UtcNow - dt).Duration() < TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Year_ExtractsYear()
    {
        var result = await _evaluator.EvaluateAsync("year(parse_date('2024-06-15'))");
        Assert.Equal(2024, Convert.ToInt32(result));
    }

    // --- Function discovery ---

    [Fact]
    public void BuiltIn_DiscoversMultipleFunctions()
    {
        var functions = ExpressionFunctionDiscovery.BuiltIn();
        Assert.True(functions.Count > 10, $"Expected >10 built-in functions, found {functions.Count}");
    }

    // --- format() function ---

    [Fact]
    public async Task Format_ReplacesNamedPlaceholders()
    {
        var parameters = new Dictionary<string, object?>
        {
            ["Name"] = "high-temp",
            ["Object"] = "92.5"
        };
        var result = await _evaluator.EvaluateStringAsync(
            "format('Temperature alert: {Name} - {Object}')", parameters);
        Assert.Equal("Temperature alert: high-temp - 92.5", result);
    }

    [Fact]
    public async Task Format_UnknownPlaceholder_LeftAsIs()
    {
        var parameters = new Dictionary<string, object?> { ["Name"] = "test" };
        var result = await _evaluator.EvaluateStringAsync(
            "format('{Name} has {Missing}')", parameters);
        Assert.Equal("test has {Missing}", result);
    }

    [Fact]
    public async Task Format_NoPlaceholders_ReturnsTemplate()
    {
        var result = await _evaluator.EvaluateStringAsync(
            "format('plain text')");
        Assert.Equal("plain text", result);
    }

    [Fact]
    public async Task Format_NullValue_ReplacedWithEmpty()
    {
        var parameters = new Dictionary<string, object?> { ["Name"] = null };
        var result = await _evaluator.EvaluateStringAsync(
            "format('Hello {Name}!')", parameters);
        Assert.Equal("Hello !", result);
    }

    [Fact]
    public async Task Format_NumericValue_FormattedInvariant()
    {
        var parameters = new Dictionary<string, object?> { ["Value"] = 3.14 };
        var result = await _evaluator.EvaluateStringAsync(
            "format('pi is {Value}')", parameters);
        Assert.Equal("pi is 3.14", result);
    }

    [Fact]
    public void BuiltIn_CachesResults()
    {
        var first = ExpressionFunctionDiscovery.BuiltIn();
        var second = ExpressionFunctionDiscovery.BuiltIn();
        Assert.Same(first, second);
    }

    [Fact]
    public void FromAssembly_ReturnsOrderedByName()
    {
        var functions = ExpressionFunctionDiscovery.BuiltIn();
        var names = functions.Select(f => f.Name).ToList();
        var sorted = names.OrderBy(n => n, StringComparer.Ordinal).ToList();
        Assert.Equal(sorted, names);
    }

    // --- format() / JSON / path ---

    [Fact]
    public async Task Format_PlaceholderWithUnderscoreAndDigit_Replaces()
    {
        var parameters = new Dictionary<string, object?> { ["Item_1"] = "X" };
        var result = await _evaluator.EvaluateStringAsync("format('v={Item_1}')", parameters);
        Assert.Equal("v=X", result);
    }

    [Fact]
    public async Task Format_UnclosedBrace_LeavesTextAsIs()
    {
        var result = await _evaluator.EvaluateStringAsync("format('a {X')", new Dictionary<string, object?>());
        Assert.Equal("a {X", result);
    }

    [Fact]
    public async Task Format_DoubleOpenBrace_NotEscaped_LiteralBracesInOutput()
    {
        // Regex only matches {word}; pin current behavior: "{{" and "ab}}" are not template escapes.
        var result = await _evaluator.EvaluateStringAsync("format('{{a}}b}}')", new Dictionary<string, object?>());
        Assert.Equal("{{a}}b}}", result);
    }

    [Fact]
    public async Task Format_DateTime_UsesInvariant()
    {
        var dt = new DateTime(2021, 5, 2, 3, 4, 0, DateTimeKind.Utc);
        var parameters = new Dictionary<string, object?> { ["D"] = dt };
        var s = await _evaluator.EvaluateStringAsync("format('x={D}')", parameters);
        Assert.Equal("x=" + dt.ToString(null, CultureInfo.InvariantCulture), s);
    }

    [Fact]
    public async Task JsonValue_DottedPathWithArrayIndex_Resolves()
    {
        var r = await _evaluator.EvaluateAsync("""json_value('{"a":{"b":[7,8]}}', 'a.b.1')""");
        Assert.Equal(8, Convert.ToInt32(r));
    }

    [Fact]
    public async Task JsonValue_MissingKey_Throws()
    {
        await Assert.ThrowsAsync<ExpressionEvaluationException>(
            () => _evaluator.EvaluateAsync("""json_value('{\"a\":1}', 'b')"""));
    }

    [Fact]
    public async Task JsonValue_NullLeaf_YieldsNull()
    {
        var r = await _evaluator.EvaluateAsync("""json_value('{\"x\":null}', 'x')""");
        Assert.Null(r);
    }

    [Fact]
    public async Task MakeJson_Pairs_ProducesParseableJson()
    {
        var json = await _evaluator.EvaluateStringAsync("make_json('a', 1, 'b', 2)");
        using var d = JsonDocument.Parse(json);
        Assert.Equal(1, d.RootElement.GetProperty("a").GetInt32());
        Assert.Equal(2, d.RootElement.GetProperty("b").GetInt32());
    }

    [Fact]
    public async Task MakeJson_JsonValue_RoundTrips()
    {
        const string expr = "json_value(make_json('k', 42, 'm', 'v'), 'k')";
        var r = await _evaluator.EvaluateAsync(expr);
        Assert.Equal(42, Convert.ToInt32(r));
    }

    // --- Date / time ---

    [Theory]
    [InlineData("2020-11-20T00:00:00Z", DateTimeKind.Utc)]
    [InlineData("2020-11-20T00:00:00+00:00", DateTimeKind.Utc)]
    [InlineData("2020-11-20T00:00:00+05:00", DateTimeKind.Utc)]
    public async Task ParseDate_ZOrOffset_ResultsInUtcKind(string s, DateTimeKind expectedKind)
    {
        var r = await _evaluator.EvaluateAsync($"parse_date('{s}')");
        var dt = Assert.IsType<DateTime>(r);
        Assert.Equal(expectedKind, dt.Kind);
    }

    [Fact]
    public async Task ParseDate_NoZone_KindUnspecified()
    {
        var r = await _evaluator.EvaluateAsync("parse_date('2020-11-20')");
        var dt = Assert.IsType<DateTime>(r);
        Assert.Equal(DateTimeKind.Unspecified, dt.Kind);
    }

    [Theory]
    [InlineData("5m", 5)]
    [InlineData("2H", 120)]
    [InlineData("1.5h", 90)]
    public async Task ParseTimespan_Suffixes(string input, int totalMinutes)
    {
        var r = await _evaluator.EvaluateAsync($"parse_timespan('{input}')");
        var ts = Assert.IsType<TimeSpan>(r);
        Assert.Equal(TimeSpan.FromMinutes(totalMinutes), ts);
    }

    [Fact]
    public async Task ParseTimespan_ClockString_FallbackParse()
    {
        var r = await _evaluator.EvaluateAsync("parse_timespan('01:02:03')");
        var ts = Assert.IsType<TimeSpan>(r);
        Assert.Equal(new TimeSpan(1, 2, 3), ts);
    }

    [Fact]
    public async Task FormatDate_UsesInvariantFormatString()
    {
        const string s = "2024-01-10T00:00:00Z";
        const string pat = "yyyy-MMM-dd";
        var t = await _evaluator.EvaluateStringAsync(
            $"""format_date(parse_date('{s}'), '{pat}')""");
        var dt = DateTime.Parse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
        var expected = dt.ToString(pat, CultureInfo.InvariantCulture);
        Assert.Equal(expected, t);
    }

    // --- Regex & segment & replace ---

    [Fact]
    public async Task RegexExtract_NoMatch_YieldsNull()
    {
        var r = await _evaluator.EvaluateAsync("""regex_extract('abc', 'z', 0)""");
        Assert.Null(r);
    }

    [Theory]
    [InlineData("''", 0, "")]
    [InlineData("'single'", 0, "single")]
    [InlineData("'a/b/'", 0, "a")]
    [InlineData("'a/b/'", 1, "b")]
    [InlineData("'a/b/'", 2, "")]
    [InlineData("'/x'", 0, "")]
    [InlineData("'/x'", 1, "x")]
    public async Task Segment_EmptyAndSlashes_Pinned(string pathExpr, int index, string expected)
    {
        var s = await _evaluator.EvaluateStringAsync($"segment({pathExpr}, {index})");
        Assert.Equal(expected, s);
    }

    [Fact]
    public async Task SegmentAt_EmptySeparator_PinsCurrentBehavior()
    {
        // .NET: Split with empty string yields one segment equal to the whole string.
        var s = await _evaluator.EvaluateStringAsync("segment_at('abc', '', 0)");
        Assert.Equal("abc", s);
    }

    // --- null-safe string; predicates; hash; base64; replace; split ---

    [Fact]
    public async Task Length_Null_RetunsZero()
    {
        var parameters = new Dictionary<string, object?> { ["n"] = null };
        var n = await _evaluator.EvaluateAsync("length([n])", parameters);
        Assert.Equal(0, Convert.ToInt32(n));
    }

    [Fact]
    public async Task OrEmpty_Trim_Tolower_Toupper_OnNull_Pinned()
    {
        var p = new Dictionary<string, object?> { ["n"] = null };
        Assert.Equal("", await _evaluator.EvaluateStringAsync("or_empty([n])", p));
        Assert.Equal("", await _evaluator.EvaluateStringAsync("trim([n])", p));
        Assert.Equal("", await _evaluator.EvaluateStringAsync("to_lower([n])", p));
        Assert.Equal("", await _evaluator.EvaluateStringAsync("to_upper([n])", p));
    }

    [Fact]
    public async Task Concat_FirstNull_SecondPresents()
    {
        var p = new Dictionary<string, object?> { ["a"] = null, ["b"] = "x" };
        var s = await _evaluator.EvaluateStringAsync("concat([a], [b])", p);
        Assert.Equal("x", s);
    }

    [Fact]
    public async Task IsNull_IsNullOrEmpty_Whitespace_HasValue_Pinned()
    {
        var withNull = new Dictionary<string, object?> { ["n"] = (object?)null };
        Assert.True(await _evaluator.EvaluateBooleanAsync("is_null([n])", withNull));
        Assert.False(await _evaluator.EvaluateBooleanAsync("is_null('')"));
        Assert.True(await _evaluator.EvaluateBooleanAsync("is_null_or_empty('')"));
        Assert.True(await _evaluator.EvaluateBooleanAsync("is_null_or_empty([n])", withNull));
        Assert.True(await _evaluator.EvaluateBooleanAsync("is_null_or_white_space('   ')"));
        Assert.True(await _evaluator.EvaluateBooleanAsync("has_value('x')"));
        Assert.False(await _evaluator.EvaluateBooleanAsync("has_value('   ')"));
    }

    [Fact]
    public async Task Base64_RoundTrips_Utf8NonAscii()
    {
        const string s = "ü";
        var parameters = new Dictionary<string, object?> { ["s"] = s };
        var round = await _evaluator.EvaluateStringAsync("base64_decode(base64_encode([s]))", parameters);
        Assert.Equal(s, round);
    }

    [Fact]
    public async Task Md5_Sha256_Sha512_MatchBcl()
    {
        var payload = "test"u8.ToArray();
        Assert.Equal(
            Convert.ToHexStringLower(MD5.HashData(payload)),
            await _evaluator.EvaluateStringAsync("""md5('test')"""));
        Assert.Equal(
            Convert.ToHexStringLower(SHA256.HashData(payload)),
            await _evaluator.EvaluateStringAsync("""sha256('test')"""));
        Assert.Equal(
            Convert.ToHexStringLower(SHA512.HashData(payload)),
            await _evaluator.EvaluateStringAsync("""sha512('test')"""));
    }

    [Fact]
    public async Task Replace_CaseInsensitive_Mixed()
    {
        var s = await _evaluator.EvaluateStringAsync("replace('aBcD', 'b', 'x')");
        Assert.Equal("axcD", s);
    }

    [Fact]
    public async Task Split_MultiCharSeparator_Splits()
    {
        var r = await _evaluator.EvaluateAsync("split('p||q||r', '||')");
        var arr = Assert.IsType<string[]>(r);
        Assert.Equal(new[] { "p", "q", "r" }, arr);
    }
}
