using Tinkwell.Bootstrapper.Expressions;

namespace Tinkwell.Bootstrapper.Tests.Expressions;

// We do not need to test Liquid implementation!
public class TemplateRenderer_Basic
{
    [Fact]
    public void TemplateRenderer_RendersStaticTemplate()
    {
        var renderer = new TemplateRenderer();
        Assert.Equal("this is a string", renderer.Render("this is a string", null).Trim());
    }

    [Fact]
    public void TemplateRenderer_RendersParametersFromObject()
    {
        var renderer = new TemplateRenderer();
        Assert.Equal("1", renderer.Render("{{value}}", new { value = 1 }).Trim());
    }

    [Fact]
    public void TemplateRenderer_RendersParametersFromDictionary()
    {
        var renderer = new TemplateRenderer();
        var parameters = new Dictionary<string, object>
        {
            { "value", 1 }
        };
        Assert.Equal("1", renderer.Render("{{value}}", parameters).Trim());
    }
}