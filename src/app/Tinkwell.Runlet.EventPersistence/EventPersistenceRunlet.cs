using System.Globalization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Tinkwell.Runner;

namespace Tinkwell.Runlet.EventPersistence;

/// <summary>
/// Runlet that persists all events from the in-process <c>EventFanOut</c>
/// to a local SQLite database. Must run in the same runner as the events
/// runlet (declared after it) so it can resolve <c>EventFanOut</c> from DI.
/// </summary>
/// <remarks>
/// <para>
/// Settings (kebab-case; see also project README) are read from runlet
/// configuration and applied with validation:
/// </para>
/// <list type="bullet">
///   <item>
///     <c>db-path</c> — SQLite database file path. Default: <c>"events.db"</c>
///     (working-directory-relative for relative paths).
///   </item>
///   <item>
///     <c>batch-size</c> — Integer; max events per write transaction. Missing
///     or unparseable values use <c>100</c>; a successfully parsed value is
///     clamped to <b>1–10,000</b> inclusive.
///   </item>
///   <item>
///     <c>flush-interval</c> — Seconds (floating-point) before flushing a
///     non-full batch. Missing, unparseable, or non-finite values use
///     <c>1.0</c>; a successfully parsed finite value is clamped to
///     <b>0.001–3600</b> seconds inclusive.
///   </item>
/// </list>
/// </remarks>
public sealed class EventPersistenceRunlet : IRunlet
{
    public void ConfigureServices(IServiceCollection services, IConfiguration settings)
    {
        const int maxBatchSize = 10_000;

        var dbPath = settings["db-path"] ?? "events.db";
        var rawBatch = int.TryParse(
            settings["batch-size"],
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var bs) ? bs : 100;
        var batchSize = Math.Clamp(rawBatch, 1, maxBatchSize);

        var rawFlush = double.TryParse(
            settings["flush-interval"],
            NumberStyles.Float | NumberStyles.AllowThousands,
            CultureInfo.InvariantCulture,
            out var fi) && double.IsFinite(fi) ? fi : 1.0;
        var flushSeconds = Math.Clamp(rawFlush, 0.001, 3600.0);

        services.AddSingleton(new EventPersistenceOptions(
            dbPath, batchSize, TimeSpan.FromSeconds(flushSeconds)));
        services.AddHostedService<EventPersistenceWorker>();
    }
}
