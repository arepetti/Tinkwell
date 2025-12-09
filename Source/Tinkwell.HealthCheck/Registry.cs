namespace Tinkwell.HealthCheck;

sealed class Registry : IRegistry
{
    public Registry(MonitoringOptions options)
    {
        _options = options;
        _samples = new(options.Samples);
    }

    public void Enqueue(DataSample item)
    {
        lock (_syncRoot)
        {
            if (_samples.Count == _options.Samples)
                _samples.RemoveAt(0);

            _samples.Add(item);
        }
    }

    public (DataSample[] Data, DataSample Average, DataQuality Quality) Snapshot()
    {
        var data = TakeSnapshot();
        if (data.Length == 0)
            return (Array.Empty<DataSample>(), DataSample.Invalid, DataQuality.Terrible);

        var average = new DataSample
        {
            CpuUsage = Average(data.Select(x => x.CpuUsage)),
            AllocatedMemory = Average(data.Select(x => x.AllocatedMemory)),
            ThreadCount = Average(data.Select(x => x.ThreadCount)),
            HandleCount = Average(data.Select(x => x.HandleCount)),
        };

        return (data, average, DataQuality.Good);
    }

    private readonly MonitoringOptions _options;
    private readonly List<DataSample> _samples;
    private readonly object _syncRoot = new();

    private DataSample[] TakeSnapshot()
    {
        lock (_syncRoot)
        {
            return _samples.ToArray(); // Oldest to newest
        }
    }

    private T Average<T>(IEnumerable<T> samples)
        where T : IConvertible
    {
        // We lose some precision for very big numbers but it should never be the
        // case for the amounts we're handling here.

        if (_options.EmaAlpha <= Double.Epsilon)
            return (T)Convert.ChangeType(samples.Average(x => Convert.ToDouble(x)), typeof(T));

        double result = Convert.ToDouble(samples.First());
        foreach (var sample in samples.Skip(1))
            result = (1 - _options.EmaAlpha) * result + _options.EmaAlpha * Convert.ToDouble(sample);

        return (T)Convert.ChangeType(result, typeof(T));
    }
}
