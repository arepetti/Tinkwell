using Tinkwell.Bootstrapper.Reflection;

namespace Tinkwell.Bootstrapper.Tests.Reflection;

public class ShallowClonerTests
{
    private class SourceClass { public int A { get; set; } public required string B { get; set; } }
    private class TargetClass { public int A { get; set; } public string B { get; set; } = ""; }

    [Fact]
    public void ShallowCloner_CopiesProperties()
    {
        var source = new SourceClass { A = 1, B = "test" };
        var target = new TargetClass();

        ShallowCloner.CopyAllPublicProperties(source, target);

        Assert.Equal(source.A, target.A);
        Assert.Equal(source.B, target.B);
    }
}
