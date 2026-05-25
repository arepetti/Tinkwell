namespace Sample.AnomalyDetector;

/// <summary>
/// Per-measure sliding window that computes running mean/stddev and
/// flags values whose z-score exceeds a threshold. This is equivalent
/// to Mahalanobis distance in one dimension — no matrix algebra needed.
/// </summary>
internal sealed class MeasureTracker
{
    private readonly double[] _buffer;
    private readonly double _threshold;
    private int _count;
    private int _index;

    public MeasureTracker(int windowSize, double threshold)
    {
        _buffer = new double[windowSize];
        _threshold = threshold;
    }

    /// <summary>
    /// Pushes a new value. Returns an <see cref="AnomalyResult"/> when
    /// the window is full and the z-score exceeds the threshold;
    /// <see langword="null"/> otherwise (still training, or value is normal).
    /// </summary>
    public AnomalyResult? Push(double value)
    {
        _buffer[_index] = value;
        _index = (_index + 1) % _buffer.Length;

        if (_count < _buffer.Length)
        {
            _count++;
            return null;
        }

        double sum = 0, sumSq = 0;
        for (int i=0; i < _buffer.Length; ++i)
        {
            sum += _buffer[i];
            sumSq += _buffer[i] * _buffer[i];
        }

        double mean = sum / _buffer.Length;
        double variance = (sumSq / _buffer.Length) - (mean * mean);

        if (variance < 1e-12)
            return null;

        double stddev = Math.Sqrt(variance);
        double zScore = Math.Abs(value - mean) / stddev;

        if (zScore > _threshold)
            return new AnomalyResult(value, zScore, mean, stddev);

        return null;
    }
}

internal sealed record AnomalyResult(
    double Value,
    double ZScore,
    double Mean,
    double StdDev);
