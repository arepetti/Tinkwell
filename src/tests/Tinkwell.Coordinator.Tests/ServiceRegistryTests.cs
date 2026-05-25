using Tinkwell.Coordinator.Configuration;
using Tinkwell.Configuration;
using Tinkwell.Configuration.Parser;
using Tinkwell.Runner;

namespace Tinkwell.Coordinator.Tests;

public class ServiceRegistryTests
{
    private static ServiceDefinition S(
        string name,
        string? family = null,
        string? alias = null) =>
        new(
            name,
            ServiceType.Grpc,
            null,
            family,
            alias is not null ? new[] { alias } : Array.Empty<string>(),
            "127.0.0.1:1",
            $"http://127.0.0.1:1/{name}");

    [Fact]
    public void Find_ExactNameBeatsAliasOnAnotherService()
    {
        var reg = new RunnerRegistry(MakeEnsemble(MakeConfig("r")));
        reg.All[0].SetServices(new[]
        {
            S("first", alias: "lookup"),
            S("lookup")
        });
        var sut = new ServiceRegistry(reg);
        var found = sut.Find("lookup");
        Assert.NotNull(found);
        Assert.Equal("lookup", found!.Name);
    }

    [Fact]
    public void Find_NameBeatsFamily()
    {
        var reg = new RunnerRegistry(MakeEnsemble(MakeConfig("r")));
        reg.All[0].SetServices(new[]
        {
            S("n1", family: "t"),
            S("t", family: "other")
        });
        var sut = new ServiceRegistry(reg);
        var found = sut.Find("t");
        Assert.NotNull(found);
        Assert.Equal("t", found!.Name);
    }

    [Fact]
    public void List_FilterMatchesFamilySubstring()
    {
        var reg = new RunnerRegistry(MakeEnsemble(MakeConfig("r")));
        reg.All[0].SetServices(new[]
        {
            S("a.b", family: "measurements"),
            S("c.d", family: "other")
        });
        var sut = new ServiceRegistry(reg);
        var list = sut.List("meas");
        Assert.Single(list);
        Assert.Equal("a.b", list[0].Name);
    }

    private static RunnerConfig MakeConfig(string name) =>
        new(name, $"runners/{name}", new Dictionary<string, ConfigValue>(), [],
            new SourceLocation("t.tw", 1, 1));

    private static EnsembleConfig MakeEnsemble(params RunnerConfig[] runners) => new(runners);
}
