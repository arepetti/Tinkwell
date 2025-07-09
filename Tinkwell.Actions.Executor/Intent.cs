using System.Diagnostics;

namespace Tinkwell.Actions.Executor;

/// <summary>
/// This is a task queued to be executed by a new instance of <c>AgentType</c>, merging the generic
/// parameters <c>Parameters</c> with the actual values obtained from the event payload.
/// </summary>
[DebuggerDisplay("{Directive.AgentType}")]
sealed class Intent
{
    public sealed class EventInfo
    {
        public required string Id { get; init; }
        public string? CorrelationId { get; init; }
        public required string Topic { get; init; }
        public required string Subject { get; init; }
        public required string Verb { get; init; }
        public string? Object { get; init; }
    }

    public required Listener Listener { get; init; }
    public required EventInfo Event { get; init; }
    public required Directive Directive { get; init; }
    public required string Payload { get; init; }
}
