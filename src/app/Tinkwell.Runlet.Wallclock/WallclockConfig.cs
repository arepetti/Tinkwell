using Microsoft.Extensions.Configuration;

namespace Tinkwell.Runlet.Wallclock;

public sealed record WallclockConfig(
    int IntervalSeconds,
    string? TimestampMeasureName,
    string? WallclockMeasureName)
{
    public static WallclockConfig Parse(IConfiguration settings)
    {
        var interval = int.TryParse(settings["interval"], out var i) && i > 0 ? i : 1;
        return new WallclockConfig(
            interval,
            ParseMeasure(settings, "timestamp", "timestamp"),
            ParseMeasure(settings, "wallclock", "wallclock"));
    }

    static string? ParseMeasure(IConfiguration settings, string key, string defaultName)
    {
        var v = settings[key];
        if (v is null)
            return defaultName;
        return v.Length == 0 ? null : v;
    }
}
