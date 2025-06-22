
namespace Tinkwell.Bootstrapper.Ensamble
{
    public interface IEnsambleConditionEvaluator
    {
        IEnumerable<RunnerDefinition> Filter(IEnumerable<RunnerDefinition> definitions);
        Dictionary<string, string?> GetParameters();
    }
}