using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Tinkwell.Runner;

namespace Tinkwell.Runlet.MqttServer;

/// <summary>
/// Runlet that hosts a minimal MQTT broker (server) for local development.
/// Clients can connect to publish/subscribe; no auth, no persistence, no telemetry.
/// </summary>
/// <remarks>
/// <para>Settings (from runlet block in ensemble):</para>
/// <list type="bullet">
///   <item><c>port</c> — TCP port (default 1883).</item>
/// </list>
/// <para><strong>Declaration order matters:</strong> If you use both the MQTT broker (server)
/// and the MQTT client runlet in the same runner, declare the <em>server</em> runlet
/// <em>before</em> the client runlet so the broker is listening when the client connects.</para>
/// </remarks>
public sealed class MqttServerRunlet : IRunlet
{
    private const int DefaultPort = 1883;

    public void ConfigureServices(IServiceCollection services, IConfiguration settings)
    {
        var port = DefaultPort;
        var portStr = settings["port"];
        if (!string.IsNullOrEmpty(portStr) && int.TryParse(portStr, out var p) && p > 0 && p < 65536)
            port = p;

        services.AddSingleton(new MqttServerRunletOptions(port));
        services.AddHostedService<MqttBrokerService>();
    }
}
