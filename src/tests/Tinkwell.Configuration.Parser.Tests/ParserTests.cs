using Tinkwell.Configuration;

namespace Tinkwell.Configuration.Parser.Tests;

public class ParserTests
{
    private readonly TestParser _parser = new();

    private Task<ConfigDocument> ParseFile(string relativePath, object? model = null)
    {
        var path = Path.Combine("TestFiles", relativePath);
        return _parser.LoadFileAsync(path, model);
    }

    [Fact]
    public async Task SimpleBlock_ParsesPropertiesCorrectly()
    {
        var doc = await ParseFile("simple-block.tw");

        var block = Assert.Single(doc.Blocks);
        Assert.Equal("config", block.Type);
        Assert.Equal("network", block.Name);
        Assert.Empty(block.Modifiers);
        Assert.Empty(block.Children);

        Assert.Equal(3, block.Properties.Count);
        Assert.Equal("use-dhcp", block.Properties[0].Key);
        Assert.Equal(new BoolValue(true), block.Properties[0].Value);
        Assert.Equal("gateway", block.Properties[1].Key);
        Assert.Equal(new StringValue("192.168.1.1"), block.Properties[1].Value);
        Assert.Equal("mtu", block.Properties[2].Key);
        Assert.Equal(new LongValue(1500), block.Properties[2].Value);
    }

    [Fact]
    public async Task NestedBlocks_ParsesHierarchyCorrectly()
    {
        var doc = await ParseFile("nested-blocks.tw");

        var runner = Assert.Single(doc.Blocks);
        Assert.Equal("runner", runner.Type);
        Assert.Equal("grpc-host", runner.Name);

        var fromMod = Assert.Single(runner.Modifiers);
        Assert.Equal("from", fromMod.Key);
        Assert.Equal(new StringValue("Tinkwell.Runners.GrpcHost"), fromMod.Value);

        Assert.Single(runner.Properties);
        Assert.Equal("port", runner.Properties[0].Key);
        Assert.Equal(new LongValue(50051), runner.Properties[0].Value);

        Assert.Equal(2, runner.Children.Count);

        var discovery = runner.Children[0];
        Assert.Equal("firmlet", discovery.Type);
        Assert.Equal("discovery", discovery.Name);
        Assert.Equal(2, discovery.Properties.Count);

        var store = runner.Children[1];
        Assert.Equal("firmlet", store.Type);
        Assert.Equal("store", store.Name);
    }

    [Fact]
    public async Task Modifiers_ParsesExpressionModifiers()
    {
        var doc = await ParseFile("modifiers.tw");

        Assert.Equal(2, doc.Blocks.Count);

        var firmlet = doc.Blocks[0];
        Assert.Equal("firmlet", firmlet.Type);
        Assert.Equal("my-service", firmlet.Name);
        var modifier = Assert.Single(firmlet.Modifiers);
        Assert.Equal("from", modifier.Key);

        var trigger = doc.Blocks[1];
        Assert.Equal("trigger", trigger.Type);
        Assert.Equal("alert", trigger.Name);
        var whenMod = Assert.Single(trigger.Modifiers);
        Assert.Equal("when", whenMod.Key);
        Assert.IsType<ExpressionValue>(whenMod.Value);
        Assert.Empty(trigger.Properties);
        Assert.Empty(trigger.Children);
    }

    [Fact]
    public async Task ValueTypes_AllTypesParseCorrectly()
    {
        var doc = await ParseFile("value-types.tw");

        var block = Assert.Single(doc.Blocks);
        var props = block.Properties.ToDictionary(p => p.Key, p => p.Value);

        Assert.Equal(new StringValue("hello world"), props["string-quoted"]);
        Assert.Equal(new StringValue("simple"), props["string-unquoted"]);
        Assert.Equal(new LongValue(42), props["number-int"]);
        Assert.Equal(new LongValue(-10), props["number-negative"]);
        Assert.Equal(new DoubleValue(3.14), props["number-double"]);
        Assert.Equal(BoolValue.True, props["bool-true"]);
        Assert.Equal(BoolValue.False, props["bool-false"]);
        Assert.Equal(new ExpressionValue("x + 1"), props["expr-at"]);
        Assert.Equal(new ExpressionValue("x > 0"), props["expr-paren"]);
    }

    [Fact]
    public async Task Comments_AreStrippedCorrectly()
    {
        var doc = await ParseFile("comments.tw");

        var block = Assert.Single(doc.Blocks);
        Assert.Equal("config", block.Type);
        Assert.Equal("test", block.Name);
        Assert.Equal(2, block.Properties.Count);
        Assert.Equal(new StringValue("value1"), block.Properties[0].Value);
        Assert.Equal(new StringValue("value2"), block.Properties[1].Value);
    }

    [Fact]
    public async Task SetAndInterpolation_RendersLiquidTemplates()
    {
        var doc = await ParseFile("set-and-interpolation.tw");

        var block = Assert.Single(doc.Blocks);
        Assert.Equal("config", block.Type);

        var props = block.Properties.ToDictionary(p => p.Key, p => p.Value);
        Assert.Equal(new StringValue("production"), props["environment"]);
        Assert.Equal(new StringValue("8080"), props["listen-port"]);
    }

    [Fact]
    public async Task IfPruning_RemovesFalseBlocks()
    {
        var doc = await ParseFile("if-pruning.tw");

        Assert.Equal(2, doc.Blocks.Count);
        Assert.Equal("always", doc.Blocks[0].Name);
        Assert.Equal("also-present", doc.Blocks[1].Name);
    }

    [Fact]
    public async Task IfPruning_NestedBlocks_PrunesCorrectlyAndStripsIfModifier()
    {
        var doc = await ParseFile("if-pruning-nested.tw");

        var app = Assert.Single(doc.Blocks);
        Assert.Equal("config", app.Type);

        // "experimental" child pruned (if false), "logging" and "metrics" kept
        Assert.Equal(2, app.Children.Count);

        var logging = app.Children[0];
        Assert.Equal("logging", logging.Name);
        // if modifier must be stripped from kept blocks
        Assert.Empty(logging.Modifiers);
        Assert.Equal(new StringValue("debug"), logging.Properties[0].Value);

        var metrics = app.Children[1];
        Assert.Equal("metrics", metrics.Name);
        Assert.Equal(new StringValue("/metrics"), metrics.Properties[0].Value);

        // Inside metrics: "console" kept, "remote" pruned (if false)
        var console = Assert.Single(metrics.Children);
        Assert.Equal("console", console.Name);
        Assert.Empty(console.Modifiers);
        Assert.Equal(new StringValue("json"), console.Properties[0].Value);
    }

    [Fact]
    public async Task SemicolonBlock_ParsesWithoutBody()
    {
        var doc = await ParseFile("semicolon-block.tw");

        var main = Assert.Single(doc.Blocks);
        Assert.Equal("firmlet", main.Type);
        Assert.Equal("main", main.Name);

        Assert.Single(main.Properties);
        var helper = Assert.Single(main.Children);
        Assert.Equal("firmlet", helper.Type);
        Assert.Equal("helper", helper.Name);
        Assert.Equal("from", helper.Modifiers[0].Key);
        Assert.Empty(helper.Properties);
    }

    [Fact]
    public async Task IncludeDirective_InlinesIncludedFile()
    {
        var path = Path.Combine("TestFiles", "includes", "main.tw");
        var doc = await _parser.LoadFileAsync(path);

        var block = Assert.Single(doc.Blocks);
        Assert.Equal("config", block.Type);
        Assert.Equal("app", block.Name);
    }

    [Fact]
    public async Task Template_ExpandsIntoUsingBlock()
    {
        var doc = await ParseFile("template.tw");

        var runner = Assert.Single(doc.Blocks);
        Assert.Equal("runner", runner.Type);
        Assert.Equal("main", runner.Name);

        Assert.Single(runner.Properties);
        Assert.Equal("option1", runner.Properties[0].Key);

        var child = Assert.Single(runner.Children);
        Assert.Equal("firmlet", child.Type);
        Assert.Equal("health-check", child.Name);
    }

    [Fact]
    public async Task ModelProperties_AreUsedInInterpolation()
    {
        var model = new { env = "staging", version = "2.0" };
        var doc = await ParseFile("model-interpolation.tw", model);

        var block = Assert.Single(doc.Blocks);
        var props = block.Properties.ToDictionary(p => p.Key, p => p.Value);

        Assert.Equal(new StringValue("staging"), props["environment"]);
        Assert.Equal(new StringValue("2.0"), props["version"]);
    }

    [Fact]
    public async Task ModelProperties_CannotBeRedefinedBySet()
    {
        var model = new { env = "staging" };
        var path = Path.Combine("TestFiles", "set-and-interpolation.tw");

        var ex = await Assert.ThrowsAsync<ConfigurationSyntaxException>(
            () => _parser.LoadFileAsync(path, model));

        Assert.Contains("Cannot redefine model property", ex.Message);
        Assert.Single(ex.Diagnostics);
    }

    [Fact]
    public async Task Properties_CarryOriginalFileAndLine()
    {
        var doc = await ParseFile("simple-block.tw");

        var block = Assert.Single(doc.Blocks);
        Assert.Equal("simple-block.tw", block.Location.FilePath);
        Assert.Equal(1, block.Location.Line);

        Assert.Equal("simple-block.tw", block.Properties[0].Location.FilePath);
        Assert.Equal(2, block.Properties[0].Location.Line);
        Assert.Equal(3, block.Properties[1].Location.Line);
        Assert.Equal(4, block.Properties[2].Location.Line);
    }

    [Fact]
    public async Task NestedBlocks_CarryOriginalFileAndLine()
    {
        var doc = await ParseFile("nested-blocks.tw");

        var runner = Assert.Single(doc.Blocks);
        Assert.Equal("nested-blocks.tw", runner.Location.FilePath);
        Assert.Equal(1, runner.Location.Line);

        Assert.Equal(2, runner.Properties[0].Location.Line);

        var discovery = runner.Children[0];
        Assert.Equal("nested-blocks.tw", discovery.Location.FilePath);
        Assert.Equal(3, discovery.Location.Line);

        var store = runner.Children[1];
        Assert.Equal(7, store.Location.Line);
    }

    [Fact]
    public async Task Include_LocationsPointToOriginalFile()
    {
        var path = Path.Combine("TestFiles", "includes-loc", "main.tw");
        var doc = await _parser.LoadFileAsync(path);

        Assert.Equal(2, doc.Blocks.Count);

        // Blocks are emitted in merged order: the included child comes first.
        var child = doc.Blocks[0];
        Assert.Equal("child-block", child.Name);
        Assert.Equal("child.tw", child.Location.FilePath);
        Assert.Equal(1, child.Location.Line);
        Assert.Equal("child.tw", child.Properties[0].Location.FilePath);
        Assert.Equal(2, child.Properties[0].Location.Line);

        var parent = doc.Blocks[1];
        Assert.Equal("parent-block", parent.Name);
        Assert.Equal("main.tw", parent.Location.FilePath);
        Assert.Equal(3, parent.Location.Line);
        Assert.Equal("main.tw", parent.Properties[0].Location.FilePath);
        Assert.Equal(4, parent.Properties[0].Location.Line);
    }

    [Fact]
    public async Task DuplicateInclude_ProducesWarningAndKeepsSingleCopy()
    {
        var path = Path.Combine("TestFiles", "includes-duplicate", "main.tw");
        var doc = await _parser.LoadFileAsync(path);

        Assert.Equal(2, doc.Blocks.Count);
        Assert.Contains(doc.Blocks, b => b.Name == "child");
        Assert.Contains(doc.Blocks, b => b.Name == "parent");

        var warning = Assert.Single(doc.Warnings);
        Assert.Equal("main.tw", warning.FileName);
        Assert.Equal(2, warning.Line);
        Assert.Contains("Duplicate include", warning.Message);
        Assert.Contains("child.tw", warning.Message);
    }

    [Fact]
    public async Task MissingInclude_ReportsCorrectParentLine()
    {
        var path = Path.Combine("TestFiles", "includes-missing", "main.tw");

        var ex = await Assert.ThrowsAsync<ConfigurationFileNotFoundException>(
            () => _parser.LoadFileAsync(path));

        Assert.Equal("does-not-exist.tw", ex.IncludePath);
        Assert.Equal("main.tw", ex.FileName);
        Assert.Equal(2, ex.Line);
    }

    [Fact]
    public async Task SyntaxErrorInIncludedFile_PointsToChild()
    {
        var path = Path.Combine("TestFiles", "includes-error", "main.tw");

        var ex = await Assert.ThrowsAsync<ConfigurationSyntaxException>(
            () => _parser.LoadFileAsync(path));

        Assert.Equal("child.tw", ex.FileName);
        Assert.True(ex.Line >= 1, $"Expected Line >= 1, got {ex.Line}");
    }

    [Fact]
    public async Task IfModifier_WithInterpolatedString_ResolvesBeforeEvaluation()
    {
        var doc = await ParseFile("if-interpolated.tw");

        Assert.Equal(2, doc.Blocks.Count);
        Assert.Equal("kept", doc.Blocks[0].Name);
        Assert.Equal("also-kept", doc.Blocks[1].Name);
        Assert.DoesNotContain(doc.Blocks, b => b.Name == "removed");
    }

    [Fact]
    public async Task UsingModifier_WithInterpolatedTemplateName_ExpandsTemplate()
    {
        var doc = await ParseFile("template-using-interpolated.tw");

        var runner = Assert.Single(doc.Blocks);
        Assert.Equal("runner", runner.Type);
        Assert.Equal("main", runner.Name);
        var child = Assert.Single(runner.Children);
        Assert.Equal("health-check", child.Name);
    }

    [Fact]
    public async Task EmptyConfiguration_ParsesToEmptyDocument()
    {
        var doc = await ParseFile("empty.tw");
        Assert.Empty(doc.Blocks);
    }

    [Fact]
    public async Task ParenExpression_WithUrlString_PreservesValue()
    {
        var doc = await ParseFile("paren-expr-strings-in-comments.tw");
        var block = Assert.Single(doc.Blocks);
        var gateway = Assert.Single(block.Properties, p => p.Key == "gateway");
        var expr = Assert.IsType<ExpressionValue>(gateway.Value);
        Assert.Equal("\"http://host/path\"", expr.Expression);
    }

    [Fact]
    public async Task LoadFileAsync_PreCanceledToken_ThrowsOperationCanceled()
    {
        var path = Path.Combine("TestFiles", "simple-block.tw");
        var token = new CancellationToken(true);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _parser.LoadFileAsync(path, null, token));
    }

    [Fact]
    public async Task ParserOptions_Lax_IsAvailableInTransform()
    {
        var p = new LaxOptionsTestParser(new ParserOptions { Lax = true });
        var path = Path.Combine("TestFiles", "simple-block.tw");
        _ = await p.LoadFileAsync(path);
        Assert.True(p.LaxObserved);
    }

    [Fact]
    public async Task UnclosedBlock_ThrowsConfigurationSyntax()
    {
        var path = Path.Combine("TestFiles", "syntax-unclosed.tw");
        await Assert.ThrowsAsync<ConfigurationSyntaxException>(
            () => _parser.LoadFileAsync(path));
    }
}

internal sealed class LaxOptionsTestParser : ConfigurationParser<ConfigDocument>
{
    public bool LaxObserved { get; private set; }

    public LaxOptionsTestParser(ParserOptions options) : base(options: options) { }

    protected override ValueTask<ConfigDocument> TransformAsync(
        ConfigDocument document, CancellationToken cancellationToken)
    {
        LaxObserved = Options.Lax;
        return ValueTask.FromResult(document);
    }
}
