namespace Tinkwell.Coap.Server.Tests;

/// <summary>
/// Minimal manually-driven <see cref="TimeProvider"/> for tests that need to control time
/// without taking a dependency on <c>Microsoft.Extensions.TimeProvider.Testing</c>. The clock
/// only moves when <see cref="Advance(TimeSpan)"/> is called; <see cref="CreateTimer"/> returns
/// an inert timer that never fires (tests drive eviction manually instead of waiting for the
/// internal periodic timer).
/// </summary>
internal sealed class TestTimeProvider : TimeProvider
{
    private DateTimeOffset _utcNow;

    public TestTimeProvider(DateTimeOffset utcNow)
    {
        _utcNow = utcNow;
    }

    public override DateTimeOffset GetUtcNow() => _utcNow;

    public void Advance(TimeSpan delta) => _utcNow = _utcNow.Add(delta);

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        => new InertTimer();

    private sealed class InertTimer : ITimer
    {
        public bool Change(TimeSpan dueTime, TimeSpan period) => true;
        public void Dispose() { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
