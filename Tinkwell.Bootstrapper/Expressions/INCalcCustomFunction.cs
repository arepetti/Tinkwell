namespace Tinkwell.Bootstrapper.Expressions;

interface INCalcCustomFunction
{
    public string Name { get; }
    object? Call(NCalc.Handlers.FunctionArgs args); 
}