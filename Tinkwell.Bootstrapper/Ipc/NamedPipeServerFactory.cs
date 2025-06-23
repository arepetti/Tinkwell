
namespace Tinkwell.Bootstrapper.Ipc;

public sealed class NamedPipeServerFactory : INamedPipeServerFactory
{
    public INamedPipeServer Create()
        => new NamedPipeServer();
}