using System.Diagnostics;

namespace Tinkwell.Expressions;

internal static class OtTraces
{
    public const string SourceName = "Tinkwell.Expressions";
    public static readonly ActivitySource Source = new(SourceName);

    public const string Evaluate = "expressions.evaluate";
}
