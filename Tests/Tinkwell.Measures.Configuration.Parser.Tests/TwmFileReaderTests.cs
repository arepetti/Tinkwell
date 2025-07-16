using Tinkwell.Measures.Configuration.Parser;

namespace Tinkwell.Bootstrapper.Ensamble.Parser.Tests;

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
        var result = await reader.ReadFromFileAsync(path, CancellationToken.None);
        await Verify(result).UseTextForParameters(Path.GetFileNameWithoutExtension(path));
    }
}