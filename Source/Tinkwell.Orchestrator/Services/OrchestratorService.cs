using Grpc.Core;
using Microsoft.Extensions.Configuration;
using Tinkwell.Bootstrapper.Ipc;
using Tinkwell.Services;

namespace Tinkwell.Orchestrator.Services;

public sealed class OrchestratorService : Tinkwell.Services.Orchestrator.OrchestratorBase
{
    public OrchestratorService(IConfiguration configuration, INamedPipeClient client)
    {
        _client = client;
        _pipeName = configuration.GetValue("Supervisor::CommandServer:PipeName",
            WellKnownNames.SupervisorCommandServerPipeName);
    }

    public override async Task<OrchestratorListReply> List(OrchestratorListRequest request, ServerCallContext context)
    {
        var response = await SendCommandAsync($"runners list \"{request.Query}\"");
        var result = new OrchestratorListReply();
        result.Runners.AddRange(response?.Split(',').Select(x => new OrchestratorListReply.Types.Runner { Name = x }));
        return result;
    }

    public override async Task<StartStopRunnerResponse> Start(StartStopRunnerRequest request, ServerCallContext context)
    {
        await SendCommandAsync($"runners start \"{request.Name}\"");
        return new StartStopRunnerResponse();
    }

    public override async Task<StartStopRunnerResponse> Stop(StartStopRunnerRequest request, ServerCallContext context)
    {
        await SendCommandAsync($"runners stop \"{request.Name}\"");
        return new StartStopRunnerResponse();
    }

    public override async Task<StartStopRunnerResponse> Restart(StartStopRunnerRequest request, ServerCallContext context)
    {
        await SendCommandAsync($"runners restart \"{request.Name}\"");
        return new StartStopRunnerResponse();
    }

    public override async Task<AddRunnerResponse> Add(AddRunnerRequest request, ServerCallContext context)
    {
        await SendCommandAsync($"runners add \"{request.Name}\" \"{request.Path}\" -- {request.Arguments}");
        return new AddRunnerResponse();
    }

    private readonly INamedPipeClient _client;
    private readonly string _pipeName;

    private async Task<string> SendCommandAsync(string command)
    {
        if (!_client.IsConnected)
            _client.Connect(_pipeName);

        var response = await _client.SendCommandAndWaitReplyAsync(command);
        return response ?? throw new InvalidOperationException("Failed to receive response from the Orchestrator.");
    }
}
