using Tinkwell.Cli.Commands.Init;

namespace Tinkwell.Cli.Commands.Init.Tests;

public class AnswerBagTests
{
    [Fact]
    public void SetAndGet_RoundTrips()
    {
        var bag = new AnswerBag();
        bag.Set("store_storage", "sqlite");
        Assert.Equal("sqlite", bag.GetString("store_storage"));
    }

    [Fact]
    public void KebabKeys_NormalizedToUnderscore()
    {
        var bag = new AnswerBag();
        bag.Set("mqtt-broker", "localhost");
        Assert.Equal("localhost", bag.GetString("mqtt_broker"));
    }

    [Fact]
    public void GetBool_ReturnsFalseForMissingKey()
    {
        var bag = new AnswerBag();
        Assert.False(bag.GetBool("missing"));
    }

    [Fact]
    public void GetInt_ReturnsDefaultForMissingKey()
    {
        var bag = new AnswerBag();
        Assert.Equal(0, bag.GetInt("missing"));
    }

    [Fact]
    public void RepeatItems_AreStored()
    {
        var bag = new AnswerBag();
        bag.AddRepeatItem("coap_resources", new Dictionary<string, object>
        {
            ["path"] = "/sensor/+",
            ["binding"] = "measure"
        });
        bag.AddRepeatItem("coap_resources", new Dictionary<string, object>
        {
            ["path"] = "/control/+",
            ["binding"] = "event"
        });

        var items = bag.GetRepeatItems("coap_resources");
        Assert.Equal(2, items.Count);
        Assert.Equal("/sensor/+", items[0]["path"]);
        Assert.Equal("event", items[1]["binding"]);
    }

    [Fact]
    public void ToTemplateContext_UsesUnderscoreKeys()
    {
        var bag = new AnswerBag();
        bag.Set("mqtt_broker", "test-host");
        bag.Set("events", true);

        var ctx = bag.ToTemplateContext(new Fluid.TemplateOptions());
        Assert.NotNull(ctx);
    }
}
