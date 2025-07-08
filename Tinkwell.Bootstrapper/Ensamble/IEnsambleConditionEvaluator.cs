
namespace Tinkwell.Bootstrapper.Ensamble;

public interface IConditionalDefinition
{
    string? Condition { get; }
}

public interface IEnsambleConditionEvaluator
{
    IEnumerable<T> Filter<T>(IEnumerable<T> definitions) where T : IConditionalDefinition;
    Dictionary<string, string?> GetParameters();
}