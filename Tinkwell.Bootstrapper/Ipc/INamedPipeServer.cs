
namespace Tinkwell.Bootstrapper.Ipc;

public interface INamedPipeServer
{
    int MaxConcurrentConnections { get; set; }

    event EventHandler? Connected;
    event EventHandler? Disconnected;
    event EventHandler<NamedPipeServerProcessEventArgs>? Process;

    void Open(string pipeName);
    void Close();
}
