using System.ComponentModel;
using Spectre.Console.Cli;

namespace Tinkwell.Cli.Commands.Store;

/// <summary>
/// Shared settings for store commands that support cross-bucket
/// filtering and the <c>--all</c> flag.
/// </summary>
internal class StoreSettings : TwCoordinatorSettings
{
    [Description("Bucket ID (required for writes, optional for reads)")]
    [CommandOption("--bucket-id|-b")]
    public string? BucketId { get; set; }

    [Description("Key namespace")]
    [CommandOption("--namespace|-s")]
    public string? Namespace { get; set; }

    [Description("Include non-discoverable (hidden) buckets")]
    [CommandOption("--all|-a")]
    [DefaultValue(true)]
    public bool All { get; set; } = true;
}
