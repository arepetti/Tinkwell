using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Tinkwell.Studio.Services;

/// <summary>
/// Lifecycle of a tracked <c>tw</c> invocation, surfaced in the Command Log
/// category. <see cref="Pending"/> covers both queued and in-flight calls
/// because Studio doesn't queue commands; everything starts running immediately.
/// </summary>
public enum CommandStatus
{
    Pending,
    Succeeded,
    Failed,
    Cancelled,
}

/// <summary>
/// A single entry shown in the Command Log: the args we passed to <c>tw</c>,
/// when it started, when it finished, and the captured stdout/stderr. Stream
/// commands (e.g. <c>tw events watch</c>) only capture stderr — their stdout
/// is consumed elsewhere as it arrives, so duplicating it here would waste memory.
/// </summary>
public sealed partial class CommandLogEntry : ObservableObject
{
    public CommandLogEntry(int id, IReadOnlyList<string> args, bool isStream, DateTimeOffset startedAt)
    {
        Id = id;
        Args = args;
        IsStream = isStream;
        StartedAt = startedAt;
    }

    public int Id { get; }

    public IReadOnlyList<string> Args { get; }

    /// <summary>
    /// True for streaming commands (<c>StreamAsync</c>): their lifetime is the
    /// duration the stream is open, not a single request/response round trip.
    /// </summary>
    public bool IsStream { get; }

    public DateTimeOffset StartedAt { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Duration))]
    [NotifyPropertyChangedFor(nameof(DurationText))]
    private DateTimeOffset? _completedAt;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRunning))]
    [NotifyPropertyChangedFor(nameof(StatusGlyph))]
    [NotifyPropertyChangedFor(nameof(StatusColorHex))]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    private CommandStatus _status = CommandStatus.Pending;

    [ObservableProperty]
    private int? _exitCode;

    [ObservableProperty]
    private string? _stdout;

    [ObservableProperty]
    private string? _stderr;

    public string CommandLine => "tw " + string.Join(' ', Args.Select(Quote));

    public string StartedAtText => StartedAt.ToLocalTime().ToString("HH:mm:ss", CultureInfo.InvariantCulture);

    public TimeSpan? Duration => CompletedAt is { } end ? end - StartedAt : null;

    public string DurationText
    {
        get
        {
            if (Duration is not { } d)
                return "—";
            return d.TotalSeconds < 1
                ? $"{d.TotalMilliseconds:N0} ms"
                : $"{d.TotalSeconds:N1} s";
        }
    }

    public bool IsRunning => Status == CommandStatus.Pending;

    public string StatusText => Status switch
    {
        CommandStatus.Pending => IsStream ? "streaming" : "running",
        CommandStatus.Succeeded => "ok",
        CommandStatus.Failed => "failed",
        CommandStatus.Cancelled => "cancelled",
        _ => "",
    };

    /// <summary>
    /// Segoe Fluent Icons glyph for the status column. Pending uses the
    /// "Sync" arc (we hide it behind a ProgressRing in XAML, but it's here for
    /// callers that prefer a static glyph); the rest are check / cancel /
    /// blocked.
    /// </summary>
    public string StatusGlyph => Status switch
    {
        CommandStatus.Pending => "\uE895",
        CommandStatus.Succeeded => "\uE73E",
        CommandStatus.Failed => "\uEA39",
        CommandStatus.Cancelled => "\uE711",
        _ => string.Empty,
    };

    public string StatusColorHex => Status switch
    {
        CommandStatus.Succeeded => CliPalette.OkHex,
        CommandStatus.Failed => CliPalette.BadHex,
        CommandStatus.Cancelled => CliPalette.WarnHex,
        _ => "#888888",
    };

    private static string Quote(string s)
        => s.Length == 0 || s.Contains(' ', StringComparison.Ordinal) ? $"\"{s}\"" : s;
}

/// <summary>
/// Append-only, dispatcher-marshalled log of every <c>tw</c> invocation Studio makes.
/// <see cref="TwCliProcessRunner"/> reports lifecycle events here; the Command
/// Log view binds <see cref="Entries"/> directly. The collection is bounded
/// (oldest entries are dropped past <see cref="MaxEntries"/>) so a long-running
/// Studio session doesn't accumulate unbounded process metadata.
/// </summary>
public sealed class CommandLog
{
    private const int MaxEntries = 500;

    private readonly IUiDispatcher _dispatcher;
    private int _nextId;

    public CommandLog(IUiDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    public ObservableCollection<CommandLogEntry> Entries { get; } = new();

    public CommandLogEntry? Begin(IReadOnlyList<string> args, bool isStream)
    {
        if (IsExcluded(args))
            return null;

        var id = Interlocked.Increment(ref _nextId);
        var entry = new CommandLogEntry(id, args.ToArray(), isStream, DateTimeOffset.UtcNow);
        _dispatcher.Post(() =>
        {
            // Newest first: typical "console history" reading order, and keeps
            // the most relevant entry visible without scrolling.
            Entries.Insert(0, entry);
            while (Entries.Count > MaxEntries)
                Entries.RemoveAt(Entries.Count - 1);
        });
        return entry;
    }

    /// <summary>
    /// Skip noisy auto-generated invocations that would otherwise drown out the
    /// user's own commands. Today this is just <c>tw ping</c> (used by both the
    /// coordinator heartbeat ticker and the Home view's connectivity probe);
    /// add new entries here as more background pollers appear.
    /// </summary>
    private static bool IsExcluded(IReadOnlyList<string> args)
        => args.Count == 1 && string.Equals(args[0], "ping", StringComparison.Ordinal);

    public void Complete(CommandLogEntry entry, int exitCode, string? stdout, string? stderr)
    {
        _dispatcher.Post(() =>
        {
            entry.Stdout = stdout;
            entry.Stderr = string.IsNullOrEmpty(stderr) ? null : stderr;
            entry.ExitCode = exitCode;
            entry.CompletedAt = DateTimeOffset.UtcNow;
            entry.Status = exitCode == 0 ? CommandStatus.Succeeded : CommandStatus.Failed;
        });
    }

    public void Cancel(CommandLogEntry entry)
    {
        _dispatcher.Post(() =>
        {
            entry.CompletedAt = DateTimeOffset.UtcNow;
            entry.Status = CommandStatus.Cancelled;
        });
    }

    public void Fail(CommandLogEntry entry, Exception ex)
    {
        _dispatcher.Post(() =>
        {
            entry.Stderr = ex.Message;
            entry.CompletedAt = DateTimeOffset.UtcNow;
            entry.Status = CommandStatus.Failed;
        });
    }

    public void Clear()
    {
        _dispatcher.Post(() => Entries.Clear());
    }
}
