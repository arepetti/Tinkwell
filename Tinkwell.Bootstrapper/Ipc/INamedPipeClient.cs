
namespace Tinkwell.Bootstrapper.Ipc
{
    public interface INamedPipeClient : IDisposable
    {
        StreamReader Reader { get; }
        StreamWriter Writer { get; }

        void Connect(string pipeName);
        void Connect(string serverName, string pipeName);
        void Disconnect();
        void SendCommand(string command);
        string? SendCommandAndWaitReply(string command);
        Task<string?> SendCommandAndWaitReplyAsync(string command, CancellationToken cancellationToken = default);
        Task<T> SendCommandAndWaitReplyAsync<T>(string command, CancellationToken cancellationToken = default);
        Task SendCommandAsync(string command, CancellationToken cancellationToken = default);
    }
}