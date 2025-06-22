namespace Tinkwell.Bootstrapper.Expressions;

public interface IExpressionEvaluator
{
    bool EvaluateBool(string expression, object? parameters);
}
