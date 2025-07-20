using Microsoft.Extensions.Configuration;
using Tinkwell.Actions.Configuration.Parser;
using Tinkwell.Bootstrapper.Expressions;

namespace Tinkwell.Bootstrapper.Ensamble.Parser.Tests;

public class TwaFileReaderTests
{
    public static IEnumerable<object[]> FilePaths()
    {
        var files = Directory
            .GetFiles("TestFiles", "*.twa")
            .OrderBy(x => x);

        foreach (var file in files)
            yield return new object[] { file };
    }

    [Theory]
    [MemberData(nameof(FilePaths))]
    public async Task CanParse(string path)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Ensamble:Params:false_setting", "false" },
            })
            .Build();

        var evaluator = new EnsambleConditionEvaluator(config, new ExpressionEvaluator());
        var reader = new TwaFileReader(evaluator);
        var result = await reader.ReadAsync(path, FileReaderOptions.Default, CancellationToken.None);
        await Verify(result)
            .UseDirectory("Snapshots")
            .UseTextForParameters(Path.GetFileNameWithoutExtension(path));
    }
}