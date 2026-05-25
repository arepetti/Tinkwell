namespace Tinkwell.Studio.Services;

public interface ICoordinatorHeartbeat
{
    CoordinatorStatus Current { get; }

    event EventHandler<CoordinatorStatus>? Changed;

    void Start();

    Task PingNowAsync(CancellationToken cancellationToken = default);
}
