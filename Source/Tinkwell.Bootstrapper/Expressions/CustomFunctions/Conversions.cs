namespace Tinkwell.Bootstrapper.Expressions.CustomFunctions;

sealed class CInt : UnaryFunction<int>
{
    protected override object? Call(int arg)
        => arg;
}

sealed class CLong : UnaryFunction<long>
{
    protected override object? Call(long arg)
        => arg;
}

sealed class CFloat : UnaryFunction<float>
{
    protected override object? Call(float arg)
        => arg;
}

sealed class CDouble : UnaryFunction<double>
{
    protected override object? Call(double arg)
        => arg;
}

sealed class CStr : UnaryFunction<string>
{
    protected override object? Call(string arg)
        => arg;
}
