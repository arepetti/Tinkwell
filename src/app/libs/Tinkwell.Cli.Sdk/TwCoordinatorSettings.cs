using System.ComponentModel;
using Spectre.Console.Cli;

namespace Tinkwell.Cli;

/// <summary>
/// Settings for commands that communicate with the Tinkwell coordinator
/// via named pipe. Extends <see cref="TwSettings"/> with the pipe address
/// and remote machine name.
/// </summary>
public class TwCoordinatorSettings : TwSettings
{
    /// <summary>Named pipe used to communicate with the coordinator.</summary>
    [Description("Coordinator pipe name")]
    [CommandOption("--pipe|-p")]
    [DefaultValue("tinkwell-coordinator")]
    public string PipeName { get; set; } = "tinkwell-coordinator";

    /// <summary>Remote machine name (<c>"."</c> for localhost).</summary>
    [Description("Remote machine name (default: localhost)")]
    [CommandOption("--machine|-m")]
    [DefaultValue(".")]
    public string Machine { get; set; } = ".";
}
