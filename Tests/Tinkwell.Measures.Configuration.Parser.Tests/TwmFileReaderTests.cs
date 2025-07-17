using Tinkwell.Bootstrapper.Ensamble;
using Tinkwell.Measures.Configuration.Parser;

namespace Tinkwell.Measures.Configuration.Parser.Tests;

public class TwmFileReaderTests
{
    public static IEnumerable<object[]> FilePaths()
    {
        var files = Directory
            .GetFiles("TestFiles", "*.twm")
            .OrderBy(x => x);

        foreach (var file in files)
            yield return new object[] { file };
    }

    [Theory]
    [MemberData(nameof(FilePaths))]
    public async Task CanParse(string path)
    {
        var reader = new TwmFileReader();
        var result = await reader.ReadAsync(path, FileReaderOptions.Default, CancellationToken.None);
        await Verify(result)
            .UseDirectory("Snapshots")
            .UseTextForParameters(Path.GetFileNameWithoutExtension(path));
    }
}