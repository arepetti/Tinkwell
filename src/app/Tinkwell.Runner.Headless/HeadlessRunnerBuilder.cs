using Tinkwell.Runner.Hosting;

namespace Tinkwell.Runner.Headless;

/// <summary>
/// Builder for headless runner containers. Accepts any <see cref="IRunlet"/>
/// implementation and builds a plain Generic Host — no web server, no gRPC.
/// </summary>
public sealed class HeadlessRunnerBuilder : RunnerHostBuilder
{
    private HeadlessRunnerBuilder(string[] args) : base(args) { }

    /// <summary>
    /// Creates a new headless runner builder from the process arguments.
    /// </summary>
    public static HeadlessRunnerBuilder Create(string[] args) => new(args);
}
