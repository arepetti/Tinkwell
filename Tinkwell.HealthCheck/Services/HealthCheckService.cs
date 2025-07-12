using Grpc.Core;
using Tinkwell.Bootstrapper.Hosting;
using Tinkwell.Services;

namespace Tinkwell.HealthCheck.Services;

public sealed class HealthCheckService : Tinkwell.Services.HealthCheck.HealthCheckBase
{
    public HealthCheckService(IRegistry registry, MonitoringOptions options)
    {
        _registry = registry;
        _options = options;
    }

    public override Task<HealthCheckResponse> Check(HealthCheckRequest request, ServerCallContext context)
    {
        return Task.FromResult(new HealthCheckResponse()
        {
            Name = HostingInformation.RunnerName,
            Status = CalculateStatus(),
        });
    }

    private readonly IRegistry _registry;
    private readonly MonitoringOptions _options;

    private HealthCheckResponse.Types.ServingStatus CalculateStatus()
    {
        var data = _registry.Snapshot();
        if (data.Quality == DataQuality.Terrible)
            return HealthCheckResponse.Types.ServingStatus.Unknown;

        bool isDegraded = data.Average.CpuUsage > _options.MaximumCpuUsage;
        return isDegraded ? HealthCheckResponse.Types.ServingStatus.Degraded : HealthCheckResponse.Types.ServingStatus.Serving;
    }
}
