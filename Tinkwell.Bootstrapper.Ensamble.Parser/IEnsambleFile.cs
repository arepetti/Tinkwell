namespace Tinkwell.Bootstrapper.Ensamble;

public interface IEnsambleFile
{
    IEnumerable<RunnerDefinition> Runners { get; }
}
