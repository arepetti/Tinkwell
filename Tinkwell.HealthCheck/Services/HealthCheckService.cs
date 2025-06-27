using Grpc.Core;
using Tinkwell.Bootstrapper;
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
        var data = _registry.Snapshot();
        bool isDegraded = data.Average.CpuUsage > _options.MaximumCpuUsage;

        var response = new HealthCheckResponse();
        response.Name = HostingInformation.RunnerName;
        response.Status = isDegraded ? HealthCheckResponse.Types.ServingStatus.Degraded : HealthCheckResponse.Types.ServingStatus.Serving;

        return Task.FromResult(response);
    }

    private readonly IRegistry _registry;
    private readonly MonitoringOptions _options;
}
