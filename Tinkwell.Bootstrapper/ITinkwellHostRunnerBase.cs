namespace Tinkwell.Bootstrapper;

public interface ITinkwellHostRunnerBase
{
    string RunnerName { get; }
    IDictionary<string, object> Properties { get; }
}
