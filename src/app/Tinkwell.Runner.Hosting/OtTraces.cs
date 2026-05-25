using System.Diagnostics;

namespace Tinkwell.Runner.Hosting;

internal static class OtTraces
{
    public const string SourceName = "Tinkwell.Runner";
    public static readonly ActivitySource Source = new(SourceName);

    public const string Lifecycle = "runner.lifecycle";
    public const string FetchConfig = "runner.fetch_config";
    public const string Initialize = "runner.initialize";
    public const string BuildHost = "runner.build_host";
    public const string StartHost = "runner.start_host";
    public const string NotifyReady = "runner.notify_ready";
    public const string LoadRunlets = "runner.load_runlets";
    public const string ValidateRunlet = "runner.validate_runlet";
    public const string StartRunlets = "runner.start_runlets";
    public const string StartRunlet = "runner.start_runlet";
    public const string StopRunlets = "runner.stop_runlets";
    public const string StopRunlet = "runner.stop_runlet";
    public const string PipeClientSend = "runner.pipe.send";
    public const string Discovery = "runner.discovery";
    public const string ChannelCreate = "runner.channel.create";

    public const string RunnerId = "runner.id";
    public const string RunnerName = "runner.name";
    public const string RunletName = "runlet.name";
    public const string RunletAssembly = "runlet.assembly";
    public const string Command = "pipe.command";
    public const string ServiceName = "service.name";
    public const string DiscoveryResult = "discovery.result";
    public const string ChannelHost = "channel.host";
    public const string ChannelCached = "channel.cached";
}
