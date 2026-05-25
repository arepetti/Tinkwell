using Tinkwell.IO;

namespace Tinkwell.Core.Tests;

public class FileTypeAnalyzerTests
{
    [Theory]
    [InlineData(new byte[] { 0x50, 0x4B, 0x03, 0x04 }, FileTypeAnalyzer.FileType.Zip)]
    [InlineData(new byte[] { 0x00, 0x61, 0x73, 0x6D }, FileTypeAnalyzer.FileType.Wasm)]
    [InlineData(new byte[] { 0x7F, 0x45, 0x4C, 0x46 }, FileTypeAnalyzer.FileType.Elf)]
    [InlineData(new byte[] { 0x4D, 0x5A, 0x00, 0x00 }, FileTypeAnalyzer.FileType.Pe)]
    [InlineData(new byte[] { 0x01, 0x02, 0x03, 0x04 }, FileTypeAnalyzer.FileType.Unknown)]
    public void Detect_Path_UsesHeader(byte[] header, FileTypeAnalyzer.FileType expected)
    {
        var path = Path.Combine(Path.GetTempPath(), "fta-" + Guid.NewGuid().ToString("N"));
        try
        {
            File.WriteAllBytes(path, header);
            Assert.Equal(expected, FileTypeAnalyzer.Detect(path));
        }
        finally
        {
            try
            {
                File.Delete(path);
            }
            catch
            {
                /* */
            }
        }
    }

    [Fact]
    public void Detect_ShortFile_ReturnsUnknown()
    {
        var path = Path.Combine(Path.GetTempPath(), "fta-short-" + Guid.NewGuid().ToString("N"));
        try
        {
            File.WriteAllBytes(path, new byte[] { 1, 2 });
            Assert.Equal(FileTypeAnalyzer.FileType.Unknown, FileTypeAnalyzer.Detect(path));
        }
        finally
        {
            try
            {
                File.Delete(path);
            }
            catch
            {
                /* */
            }
        }
    }

    [Fact]
    public void IsExecutableOrObject_RecognizesKnownExtension()
    {
        var path = Path.Combine(Path.GetTempPath(), "dummy-" + Guid.NewGuid().ToString("N") + ".dll");
        try
        {
            File.WriteAllText(path, "not really a PE");
            Assert.True(FileTypeAnalyzer.IsExecutableOrObject(path));
        }
        finally
        {
            try
            {
                File.Delete(path);
            }
            catch
            {
                /* */
            }
        }
    }
}
