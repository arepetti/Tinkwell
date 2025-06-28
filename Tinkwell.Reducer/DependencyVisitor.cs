using NCalc.Domain;
using NCalc.Visitors;

namespace Tinkwell.Reducer;

public class DependencyVisitor : EvaluationVisitor
{
    public HashSet<string> Dependencies { get; } = new();

    public DependencyVisitor(NCalc.ExpressionContext context)
        : base(context)
    {
    }

    public override object? Visit(Identifier identifier)
    {
        Dependencies.Add(identifier.Name);
        return base.Visit(identifier);
    }
}
