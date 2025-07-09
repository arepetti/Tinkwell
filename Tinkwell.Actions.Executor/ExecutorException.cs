namespace Tinkwell.Actions.Executor;

public sealed class ExecutorException : Exception
{
    public ExecutorException(string message) : base(message) { }
    public ExecutorException(string message, Exception innerException) : base(message, innerException) { }
}