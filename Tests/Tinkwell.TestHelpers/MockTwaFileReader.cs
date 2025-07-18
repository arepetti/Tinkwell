using Tinkwell.Actions.Configuration.Parser;
using Tinkwell.Bootstrapper.Ensamble;

namespace Tinkwell.TestHelpers;

public sealed class MockTwaFile : ITwaFile
{
    public IEnumerable<WhenDefinition> Listeners { get; init; } = new List<WhenDefinition>();
}

public sealed class MockTwaFileReader : IConfigFileReader<ITwaFile>
{
    private readonly MockTwaFile _file = new();

    public MockTwaFileReader(params WhenDefinition[] listeners)
    {
        _file = new MockTwaFile { Listeners = listeners.ToList() };
    }

    public Task<ITwaFile> ReadAsync(string path, FileReaderOptions options, CancellationToken cancellationToken)
    {
        return Task.FromResult<ITwaFile>(_file);
    }
}