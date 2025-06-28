namespace Tinkwell.Reducer.Parser;

sealed class MeasureListConfigReader
{
    public async Task<IEnumerable<DerivedMeasure>> ReadFromFileAsync(string filename, CancellationToken cancellationToken)
    {
        var content = await File.ReadAllTextAsync(filename, cancellationToken);
        if (cancellationToken.IsCancellationRequested)
            return Enumerable.Empty<DerivedMeasure>();

        return MeasureListConfigParser.Parse(content).ToList();
    }
}
