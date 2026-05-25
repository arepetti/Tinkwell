using Tinkwell.Cli.Commands.Init;

namespace Tinkwell.Cli.Commands.Init.Tests;

public class WizardPackParserTests
{
    [Fact]
    public async Task LoadAsync_ParsesDefaultPack()
    {
        var packDir = FindPackDirectory("tinkwell-ensemble");
        var pack = await WizardPackParser.LoadAsync(packDir);

        Assert.Equal("tinkwell-ensemble", pack.Name);
        Assert.Equal("ensemble.tw", pack.PrimaryOutput);
        Assert.NotEmpty(pack.Questions.Nodes);
        Assert.NotEmpty(pack.Outputs);
    }

    [Fact]
    public async Task LoadAsync_ParsesTopologyQuestion()
    {
        var packDir = FindPackDirectory("tinkwell-ensemble");
        var pack = await WizardPackParser.LoadAsync(packDir);

        var topology = pack.Questions.Nodes.OfType<QuestionDef>()
            .FirstOrDefault(q => q.Id == "topology");

        Assert.NotNull(topology);
        Assert.Equal(QuestionType.Choice, topology.Type);
        Assert.Equal(3, topology.Options.Count);
        Assert.Null(topology.WhenCondition);
    }

    [Fact]
    public async Task LoadAsync_ParsesConditionalQuestion()
    {
        var packDir = FindPackDirectory("tinkwell-ensemble");
        var pack = await WizardPackParser.LoadAsync(packDir);

        var eventPersistence = pack.Questions.Nodes.OfType<QuestionDef>()
            .FirstOrDefault(q => q.Id == "event_persistence");

        Assert.NotNull(eventPersistence);
        Assert.NotNull(eventPersistence.WhenCondition);
        Assert.Contains("events", eventPersistence.WhenCondition);
    }

    [Fact]
    public async Task LoadAsync_ParsesRepeatGroup()
    {
        var packDir = FindPackDirectory("tinkwell-ensemble");
        var pack = await WizardPackParser.LoadAsync(packDir);

        var repeat = pack.Questions.Nodes.OfType<RepeatGroup>().FirstOrDefault();

        Assert.NotNull(repeat);
        Assert.Equal("coap_resources", repeat.Id);
        Assert.Equal("resource", repeat.ItemName);
        Assert.NotNull(repeat.Count);
        Assert.True(repeat.Questions.Count >= 2);
    }

    [Fact]
    public async Task LoadAsync_ParsesOutputWithValidator()
    {
        var packDir = FindPackDirectory("tinkwell-ensemble");
        var pack = await WizardPackParser.LoadAsync(packDir);

        var ensembleOutput = pack.Outputs.FirstOrDefault(o => o.Id == "ensemble");

        Assert.NotNull(ensembleOutput);
        Assert.Equal("ensemble.tw", ensembleOutput.Path);
        Assert.Equal("ensemble.liquid", ensembleOutput.RenderTemplate);
        Assert.Equal("tinkwell-ensemble", ensembleOutput.Validator);
    }

    private static string FindPackDirectory(string packName)
    {
        var dir = AppContext.BaseDirectory;
        var path = Path.Combine(dir, "packs", "init", packName);
        if (Directory.Exists(path))
            return path;

        var current = new DirectoryInfo(dir);
        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, "src", "Tinkwell.Cli.Commands.Init",
                "packs", "init", packName);
            if (Directory.Exists(candidate))
                return candidate;
            current = current.Parent;
        }

        throw new DirectoryNotFoundException(
            $"Could not find pack directory '{packName}'. Ensure the pack is copied to the output directory.");
    }
}
