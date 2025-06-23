using System.Collections.Concurrent;

namespace Tinkwell.Store.Storage;

sealed class HistoryDictionary<TMetadata, TValue> where TMetadata : IStorageMetadata
{
    private readonly ConcurrentDictionary<string, Entry> _store = new(StringComparer.Ordinal);
    private readonly IEqualityComparer<TValue> _equalityComparer;

    public interface IEntry
    {
        TMetadata Metadata { get; }
        IEnumerable<TValue> GetHistory();
        bool TryGetValue(out TValue? value);
    }

    public record CreateEntryOptions(int? MaxHistorySize, TimeSpan? Expiration);

    public HistoryDictionary(IEqualityComparer<TValue> valueEqualityComparer)
    {
        _equalityComparer = valueEqualityComparer ?? throw new ArgumentNullException(nameof(valueEqualityComparer));
    }

    public event EventHandler<ValueChangedEventArgs<TValue>>? ValueChanged;

    public bool Register(TMetadata metadata, CreateEntryOptions options)
        => _store.TryAdd(metadata.Key, new Entry(metadata, options));

    public void Update(string name, TValue value)
    {
        TValue? oldValue = default;
        _store.AddOrUpdate(name, Create, Update);

        if (!_equalityComparer.Equals(oldValue, value))
            ValueChanged?.Invoke(this, new ValueChangedEventArgs<TValue>(name, oldValue, value));

        Entry Create(string _)
            => throw new InvalidOperationException("Cannot add a new entry with a value. Use Register() first.");

        Entry Update(string key, Entry entry)
        {
            TryGetValue(key, out oldValue);
            return entry.Update(value);
        }
    }

    public IEnumerable<IEntry> List()
        => _store.Values.ToArray();

    public bool TryGetEntry(string name, out IEntry? entry)
    {
        if (_store.TryGetValue(name, out var e))
        {
            entry = e;
            return true;
        }

        entry = default;
        return false;
    }

    private bool TryGetValue(string name, out TValue? value)
    {
        if (_store.TryGetValue(name, out var entry))
        {
            if (entry.TryGetValue(out value))
                return true;
        }

        value = default;
        return false;
    }

    record TimestampedValue(TValue Value, DateTime Timestamp);

    sealed class Entry(TMetadata metadata, CreateEntryOptions options) : IEntry
    {
        private readonly CreateEntryOptions _options = options;
        private readonly ConcurrentQueue<TimestampedValue> _history = new();

        public Entry(TMetadata metadata, CreateEntryOptions options, TValue initialValue)
            : this(metadata, options)
        {
            _history.Enqueue(new TimestampedValue(initialValue, DateTime.UtcNow));
        }

        public TMetadata Metadata { get; init; } = metadata;

        public IEnumerable<TValue> GetHistory()
        {
            // Note that, to avoid using a ReadWriterLockSlic or a lock(),
            // we re using a ConcurrentQueue but add + clean up is not an atomic operation!
            // When we fetch the history we could get a longer array because the oldest element
            // is still present. No crashes but the history "could" be longer than expected.
            return _history.ToArray().Select(x => x.Value);
        }

        public bool TryGetValue(out TValue? value)
        {
            // TODO: to read the latest value is probably very common and Last() takes
            // a snapshot. Not exactly fast! Maybe a simple concurrent linked list works better.
            var latest = _history.LastOrDefault();
            if (latest is not null)
            {
                value = latest.Value;
                return true;
            }

            value = default;
            return false;
        }

        public Entry Update(TValue value)
        {
            _history.Enqueue(new TimestampedValue(value, DateTime.UtcNow));
            Cleanup();

            return this;
        }

        private void Cleanup()
        {
            if (_options.MaxHistorySize is not null && _history.Count > _options.MaxHistorySize)
                _history.TryDequeue(out var _);

            if (_options.Expiration is null)
                return;

            var threshold = DateTime.UtcNow - _options.Expiration.Value;
            while (_history.TryPeek(out var oldest) && oldest.Timestamp < threshold)
                _history.TryDequeue(out var _);
        }
    }
}
