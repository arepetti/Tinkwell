using Tinkwell;

namespace Tinkwell.Runner.Hosting;

/// <summary>
/// Command-line options passed by the coordinator when launching a runner process.
/// </summary>
public sealed class RunnerOptions
{
    public required string RunnerId { get; init; }
    public required string CoordinatorPipe { get; init; }
    public required string SentinelPipe { get; init; }

    /// <summary>
    /// Parses runner options from the command-line arguments injected by the coordinator.
    /// Expected flags: <c>--runner-id</c>, <c>--coordinator-pipe</c>, <c>--sentinel-pipe</c>.
    /// </summary>
    public static RunnerOptions Parse(string[] args)
    {
        string? runnerId = null, coordinatorPipe = null, sentinelPipe = null;

        for (int i=0; i < args.Length - 1; ++i)
        {
            switch (args[i])
            {
                case "--runner-id":
                    runnerId = args[++i];
                    break;
                case "--coordinator-pipe":
                    coordinatorPipe = args[++i];
                    break;
                case "--sentinel-pipe":
                    sentinelPipe = args[++i];
                    break;
            }
        }

        if (!ShortIdGenerator.IsValid(runnerId))
            throw new ArgumentException("Missing or invalid --runner-id");
        if (string.IsNullOrWhiteSpace(coordinatorPipe))
            throw new ArgumentException("Missing --coordinator-pipe");
        if (string.IsNullOrWhiteSpace(sentinelPipe))
            throw new ArgumentException("Missing --sentinel-pipe");

        return new RunnerOptions
        {
            RunnerId = runnerId!,
            CoordinatorPipe = coordinatorPipe,
            SentinelPipe = sentinelPipe
        };
    }
}
