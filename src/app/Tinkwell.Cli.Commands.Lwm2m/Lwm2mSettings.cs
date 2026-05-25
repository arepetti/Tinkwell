using System.ComponentModel;
using Spectre.Console.Cli;
using Tinkwell.Cli;

namespace Tinkwell.Cli.Commands.Lwm2m;

/// <summary>
/// Shared settings for all LwM2M commands.
/// </summary>
public class Lwm2mSettings : TwSettings
{
    [Description("Target LwM2M server host")]
    [CommandOption("--host|-H")]
    [DefaultValue("localhost")]
    public string Host { get; set; } = "localhost";

    [Description("Target CoAP port")]
    [CommandOption("--port")]
    [DefaultValue(5683)]
    public int Port { get; set; } = 5683;

    [Description("Response timeout in seconds")]
    [CommandOption("--timeout|-t")]
    [DefaultValue(5)]
    public int Timeout { get; set; } = 5;
}
