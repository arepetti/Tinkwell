namespace Tinkwell.Bootstrapper.Expressions;

public interface IExpressionEvaluator
{
    object? Evaluate(string expression, object? parameters);
    bool EvaluateBool(string expression, object? parameters);
    string EvaluateString(string expression, object? parameters);
}
