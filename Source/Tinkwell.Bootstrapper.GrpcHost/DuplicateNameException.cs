namespace Tinkwell.Bootstrapper.GrpcHost;

public sealed class DuplicateNameException : ArgumentException
{
    public DuplicateNameException(string message) : base(message) { }
}
