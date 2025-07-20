using System.Collections.Concurrent;

namespace Tinkwell.Supervisor.Sentinel;

sealed class EventCounter
{
    private readonly ConcurrentQueue<DateTime> _events = new();
    
    public void Increment()
        => _events.Enqueue(DateTime.UtcNow);

    public int PeekCount()
        => _events.Count;

    public int CountIn(int seconds)
    {
        DateTime intervalStart = DateTime.UtcNow.AddSeconds(-seconds);

        while (_events.TryPeek(out DateTime oldest) && oldest < intervalStart)
            _events.TryDequeue(out _);

        return _events.Count;
    }

    public void Clear()
        => _events.Clear();
}