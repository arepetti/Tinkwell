namespace Tinkwell.Bootstrapper;

[Serializable]
public sealed class BootstrapperException : Exception
{
    public BootstrapperException(string message) : base(message) { }
    public BootstrapperException(string message, Exception innerException) : base(message, innerException) { }
}

