namespace Tinkwell.Orchestrator.Services;

public interface ICommandServerClient
{
    Task<string> SendCommandAsync(string command);
}
