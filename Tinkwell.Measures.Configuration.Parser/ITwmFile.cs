namespace Tinkwell.Measures.Configuration.Parser;

public interface ITwmFile
{
    IEnumerable<MeasureDefinition> Measures { get; }

    IEnumerable<SignalDefinition> Signals { get; }
}
