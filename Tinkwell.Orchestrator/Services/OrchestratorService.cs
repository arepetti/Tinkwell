using Grpc.Core;
using Tinkwell.Services;

namespace Tinkwell.Orchestrator.Services;

public sealed class OrchestratorService : Tinkwell.Services.Orchestrator.OrchestratorBase
{
    private readonly ICommandServerClient _client;

    public OrchestratorService(ICommandServerClient client)
    {
        _client = client;
    }

    public override async Task<OrchestratorListReply> List(OrchestratorListRequest request, ServerCallContext context)
    {
        var response = await _client.SendCommandAsync($"runners list \"{request.Query}\"");
        var result = new OrchestratorListReply();
        result.Runners.AddRange(response.Split(',').Select(x => new OrchestratorListReply.Types.Runner { Name = x }));
        return result;
    }

    public override async Task<StartStopRunnerResponse> Start(StartStopRunnerRequest request, ServerCallContext context)
    {
        await _client.SendCommandAsync($"runners start \"{request.Name}\"");
        return new StartStopRunnerResponse();
    }

    public override async Task<StartStopRunnerResponse> Stop(StartStopRunnerRequest request, ServerCallContext context)
    {
        await _client.SendCommandAsync($"runners stop \"{request.Name}\"");
        return new StartStopRunnerResponse();
    }

    public override async Task<StartStopRunnerResponse> Restart(StartStopRunnerRequest request, ServerCallContext context)
    {
        await _client.SendCommandAsync($"runners restart \"{request.Name}\"");
        return new StartStopRunnerResponse();
    }

    public override async Task<AddRunnerResponse> Add(AddRunnerRequest request, ServerCallContext context)
    {
        await _client.SendCommandAsync($"runners add \"{request.Name}\" \"{request.Path}\" -- {request.Arguments}");
        return new AddRunnerResponse();
    }
}
