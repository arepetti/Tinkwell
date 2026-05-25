using Tinkwell.Cli.Commands.Init;

namespace Tinkwell.Cli.Commands.Init.Tests;

public class TemplateRendererTests
{
    [Fact]
    public void Render_SubstitutesScalarValues()
    {
        var bag = new AnswerBag();
        bag.Set("name", "test");

        var result = TemplateRenderer.Render("Hello {{ name }}!", bag);
        Assert.Equal("Hello test!", result);
    }

    [Fact]
    public void Render_HandlesConditionals()
    {
        var bag = new AnswerBag();
        bag.Set("events", true);

        var template = "{% if events %}events enabled{% endif %}";
        var result = TemplateRenderer.Render(template, bag);
        Assert.Equal("events enabled", result);
    }

    [Fact]
    public void Render_HandlesConditionals_WhenFalse()
    {
        var bag = new AnswerBag();
        bag.Set("events", false);

        var template = "{% if events %}events enabled{% endif %}";
        var result = TemplateRenderer.Render(template, bag);
        Assert.Equal("", result);
    }

    [Fact]
    public void Render_IteratesRepeatGroups()
    {
        var bag = new AnswerBag();
        bag.AddRepeatItem("items", new Dictionary<string, object>
        {
            ["name"] = "alpha"
        });
        bag.AddRepeatItem("items", new Dictionary<string, object>
        {
            ["name"] = "beta"
        });

        var template = "{% for item in items %}{{ item.name }}\n{% endfor %}";
        var result = TemplateRenderer.Render(template, bag);
        Assert.Contains("alpha", result);
        Assert.Contains("beta", result);
    }

    [Fact]
    public async Task Render_BalancedTopology_ProducesRunnerBlocks()
    {
        var bag = new AnswerBag();
        bag.Set("topology", "balanced");
        bag.Set("store_storage", "memory");
        bag.Set("events", true);
        bag.Set("measures", true);

        var templatePath = FindPackTemplate("ensemble.liquid");
        var result = await TemplateRenderer.RenderAsync(templatePath, bag);

        Assert.Contains("runner grpc-services from", result);
        Assert.Contains("runlet store from", result);
        Assert.Contains("runlet events from", result);
        Assert.Contains("runlet measures from", result);
    }

    [Fact]
    public async Task Render_CompactTopology_ProducesSingleRunner()
    {
        var bag = new AnswerBag();
        bag.Set("topology", "compact");
        bag.Set("store_storage", "memory");
        bag.Set("events", false);
        bag.Set("measures", false);

        var templatePath = FindPackTemplate("ensemble.liquid");
        var result = await TemplateRenderer.RenderAsync(templatePath, bag);

        Assert.Contains("runner main from", result);
        Assert.DoesNotContain("runner grpc-services", result);
    }

    [Fact]
    public async Task Render_ReliableTopology_SeparatesServices()
    {
        var bag = new AnswerBag();
        bag.Set("topology", "reliable");
        bag.Set("store_storage", "sqlite");
        bag.Set("events", true);
        bag.Set("measures", true);

        var templatePath = FindPackTemplate("ensemble.liquid");
        var result = await TemplateRenderer.RenderAsync(templatePath, bag);

        Assert.Contains("runner store-host from", result);
        Assert.Contains("runner events-host from", result);
        Assert.Contains("runner measures-host from", result);
    }

    [Fact]
    public async Task Render_Mqtt_ProducesBindingConfig()
    {
        var bag = new AnswerBag();
        bag.Set("topology", "balanced");
        bag.Set("store_storage", "memory");
        bag.Set("mqtt", true);
        bag.Set("mqtt_broker", "test-broker");
        bag.Set("mqtt_port", 1883);
        bag.Set("mqtt_topic", "sensor/+");
        bag.Set("mqtt_binding", "event");

        var templatePath = FindPackTemplate("ensemble.liquid");
        var result = await TemplateRenderer.RenderAsync(templatePath, bag);

        Assert.Contains("mqtt broker", result);
        Assert.Contains("broker = \"test-broker\"", result);
        Assert.Contains("bind event", result);
    }

    [Fact]
    public async Task Render_CoAPWithResources_ProducesResourceBlocks()
    {
        var bag = new AnswerBag();
        bag.Set("topology", "balanced");
        bag.Set("store_storage", "memory");
        bag.Set("coap", true);
        bag.Set("coap_port", 5683);
        var resource = new Dictionary<string, object>
        {
            ["path"] = "/sensor/+",
            ["binding"] = "measure"
        };
        bag.AddRepeatItem("coap_resources", resource);

        var templatePath = FindPackTemplate("ensemble.liquid");
        var result = await TemplateRenderer.RenderAsync(templatePath, bag);

        Assert.Contains("coap sensors", result);
        Assert.Contains("port = 5683", result);
        Assert.Contains("resource \"/sensor/+\"", result);
        Assert.Contains("bind measure", result);
    }

    [Fact]
    public async Task Render_DataRouting_MeasuresToMqtt()
    {
        var bag = new AnswerBag();
        bag.Set("topology", "balanced");
        bag.Set("store_storage", "memory");
        bag.Set("mqtt", true);
        bag.Set("mqtt_broker", "broker");
        bag.Set("mqtt_port", 1883);
        bag.Set("mqtt_topic", "sensor/+");
        bag.Set("mqtt_binding", "event");
        bag.Set("measures", true);
        bag.Set("modbus", true);
        bag.Set("route_measures_to_mqtt", true);
        bag.Set("route_measures_mqtt_topic", "tw/measures/{name}");

        var templatePath = FindPackTemplate("ensemble.liquid");
        var result = await TemplateRenderer.RenderAsync(templatePath, bag);

        Assert.Contains("action forward-measures-to-mqtt", result);
        Assert.Contains("runlet measure-events from", result);
        Assert.Contains("runlet actions from", result);
    }

    [Fact]
    public async Task Render_Wallclock_IncludedWithStateMachines()
    {
        var bag = new AnswerBag();
        bag.Set("topology", "balanced");
        bag.Set("store_storage", "memory");
        bag.Set("measures", true);
        bag.Set("statemachines", true);

        var templatePath = FindPackTemplate("ensemble.liquid");
        var result = await TemplateRenderer.RenderAsync(templatePath, bag);

        Assert.Contains("runlet wallclock from", result);
        Assert.Contains("runlet statemachines from", result);
    }

    private static string FindPackTemplate(string fileName)
    {
        var dir = AppContext.BaseDirectory;
        var path = Path.Combine(dir, "packs", "init", "tinkwell-ensemble", fileName);
        if (File.Exists(path))
            return path;

        var current = new DirectoryInfo(dir);
        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, "src", "Tinkwell.Cli.Commands.Init",
                "packs", "init", "tinkwell-ensemble", fileName);
            if (File.Exists(candidate))
                return candidate;
            current = current.Parent;
        }

        throw new FileNotFoundException(
            $"Could not find pack template '{fileName}'. Ensure the pack is copied to the output directory.");
    }
}
