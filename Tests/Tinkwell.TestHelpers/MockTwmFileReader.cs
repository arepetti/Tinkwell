using Tinkwell.Bootstrapper.Ensamble;
using Tinkwell.Measures.Configuration.Parser;

namespace Tinkwell.TestHelpers;

public sealed class MockTwmFileReader : IConfigFileReader<ITwmFile>
{
    public void AddMeasure(string name, string quantityType, string unit, string expression)
    {
        _measures.Add(new MeasureDefinition
        {
            Name = name,
            QuantityType = quantityType,
            Unit = unit,
            Expression = expression
        });
    }

    public void AddScalar(string name, string expression)
        => AddMeasure(name, "Scalar", "", expression);

    public Task<ITwmFile> ReadAsync(string path, FileReaderOptions options, CancellationToken cancellationToken)
        => Task.FromResult<ITwmFile>(new TwmFile(_measures));

    private readonly List<MeasureDefinition> _measures = new();

    private sealed class TwmFile(IEnumerable<MeasureDefinition> measures) : ITwmFile
    {
        public IEnumerable<MeasureDefinition> Measures { get; } = measures;

        public IEnumerable<SignalDefinition> Signals { get; } = new List<SignalDefinition>();
    }
}
