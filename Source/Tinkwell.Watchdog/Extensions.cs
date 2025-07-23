using System.Collections.Concurrent;
using Tinkwell.Services;

namespace Tinkwell.Watchdog;

static class Extensions
{
    public static ServiceStatus Translate(this HealthCheckResponse.Types.ServingStatus status)
    {
        return status switch
        {
            HealthCheckResponse.Types.ServingStatus.Serving => ServiceStatus.Serving,
            HealthCheckResponse.Types.ServingStatus.Degraded => ServiceStatus.Degraded,
            HealthCheckResponse.Types.ServingStatus.NotServing => ServiceStatus.Crashed,
            _ => ServiceStatus.Unknown
        };
    }
}
