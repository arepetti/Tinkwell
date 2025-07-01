using Tinkwell.Measures.Configuration.Parser;

namespace Tinkwell.Reactor;

sealed class SignalListConfigReader : ConfigListReaderBase
{
    public async Task<IEnumerable<Signal>> ReadFromFileAsync(
        string filename,
        CancellationToken cancellationToken)
    {
        var signals = new List<Signal>();
        var baseDirectory = Path.GetDirectoryName(filename);

        await ReadFileRecursiveAsync(filename, baseDirectory, signals, cancellationToken);

        return signals;
    }

    private static async Task ReadFileRecursiveAsync(string filename, string? baseDirectory, List<Signal> signals, CancellationToken cancellationToken)
    {
        var content = await File.ReadAllTextAsync(filename, cancellationToken);
        if (cancellationToken.IsCancellationRequested)
            return;

        var entries = MeasureListConfigParser<MeasureDefinition, SignalDefinition>.Parse(content);
        
        signals.AddRange(entries.OfType<SignalDefinition>().Select(x => ToSignal(null, x)));
        foreach (var measure in entries.OfType<MeasureDefinition>())
            signals.AddRange(measure.Signals.Select(x => ToSignal(measure, x)));

        foreach (var import in entries.OfType<ImportDirective>())
        {
            if (cancellationToken.IsCancellationRequested)
                return;

            var path = ResolveImportPath(baseDirectory, import);
            await ReadFileRecursiveAsync(path, Path.GetDirectoryName(path), signals, cancellationToken);
        }
    }

    private static Signal ToSignal(MeasureDefinition? associatedMeasure, SignalDefinition signalDefinition)
    {
        return new Signal
        {
            Name = signalDefinition.Name,
            When = signalDefinition.When,
            Topic = signalDefinition.Topic,
            Payload = signalDefinition.Payload,
            Owner = associatedMeasure?.Name,
        };
    }
}