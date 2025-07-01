namespace Tinkwell.Reactor;

sealed class ReactorOptions
{
    public required string Path { get; init; }
    public required bool CheckOnStartup { get; init; }
}
