using Fluid;
using Tinkwell.Cli.Commands.Init;

namespace Tinkwell.Cli.Commands.Init.Tests;

public class FluidSanityTests
{
    [Theory]
    [InlineData("{% if x %}yes{% endif %}", true)]
    [InlineData("{% if x %}A{% endif %}{% if y %}B{% endif %}", true)]
    [InlineData("{% for item in items %}{{ item.name }}{% endfor %}", true)]
    [InlineData("{{ name }}", true)]
    [InlineData("{% if x or y %}yes{% endif %}", true)]
    public void FluidParser_ParsesTag(string template, bool expectedValid)
    {
        var parser = new FluidParser();
        var ok = parser.TryParse(template, out _, out var error);
        Assert.Equal(expectedValid, ok);
        if (expectedValid)
            Assert.Null(error);
    }

    [Theory]
    [InlineData("{{ x | upcase }}", "HELLO")]
    [InlineData("{{ x | append: \" world\" }}", "hello world")]
    [InlineData("{% assign y = x | upcase %}{{ y }}", "HELLO")]
    [InlineData("{% assign y = x | append: \" world\" %}{{ y }}", "hello world")]
    public void Filter_VariousContexts(string template, string expected)
    {
        var bag = new AnswerBag();
        bag.Set("x", "hello");
        var result = TemplateRenderer.Render(template, bag);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void NativeEquality_InIfTag_Renders()
    {
        var bag = new AnswerBag();
        bag.Set("topology", "balanced");
        var result = TemplateRenderer.Render(
            "{% if topology == \"balanced\" %}yes{% endif %}", bag);
        Assert.Equal("yes", result);
    }

    [Fact]
    public void NativeInequality_InIfTag_Renders()
    {
        var bag = new AnswerBag();
        bag.Set("topology", "compact");
        var result = TemplateRenderer.Render(
            "{% if topology != \"balanced\" %}yes{% endif %}", bag);
        Assert.Equal("yes", result);
    }

    [Fact]
    public void NativeEquality_Elsif_Renders()
    {
        var bag = new AnswerBag();
        bag.Set("topology", "compact");
        var result = TemplateRenderer.Render(
            "{% if topology == \"balanced\" %}B{% elsif topology == \"compact\" %}C{% else %}R{% endif %}", bag);
        Assert.Equal("C", result);
    }

    [Fact]
    public void NativeEquality_InRepeatItem_Renders()
    {
        var bag = new AnswerBag();
        bag.AddRepeatItem("items", new Dictionary<string, object> { ["binding"] = "measure" });
        bag.AddRepeatItem("items", new Dictionary<string, object> { ["binding"] = "event" });
        var result = TemplateRenderer.Render(
            "{% for item in items %}{% if item.binding == \"measure\" %}M{% endif %}{% endfor %}", bag);
        Assert.Equal("M", result);
    }
}
