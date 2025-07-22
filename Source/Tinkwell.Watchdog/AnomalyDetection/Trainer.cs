namespace Tinkwell.Watchdog.AnomalyDetection;

sealed class Trainer
{
    private const int MiniumNumberOfSamplesForTraining = 40;
    private const int MaximumNumberOfSamplesForTraining = MiniumNumberOfSamplesForTraining * 2;

    public bool? Enqueue(Snapshot[] snapshots)
    {
        var goodSamples = snapshots.Where(x => x.Quality >= SnapshotQuality.Acceptable);
        if (!goodSamples.Any())
            return null;

        var cpu = goodSamples.Sum(x => x.CpuUsage);
        var memory = goodSamples.Sum(x => x.Memory);
        var threads = goodSamples.Sum(x => x.ThredCount);
        var handles = goodSamples.Sum(x => x.HandleCount);

        var sample = new Sample(cpu, memory, threads, handles);
        if (_isTrained)
            return _detector.IsAnomalous(sample);

        _samples.Add(sample);

        if (_samples.Count >= MiniumNumberOfSamplesForTraining)
        {
            _isTrained = _detector.Train(_samples);

            // If the training fails (for example the matrix is singular) then we
            // also try to keep adding samples but only up to a point. Reached that we
            // clear the data and start over.
            if (!_isTrained && _samples.Count >= MaximumNumberOfSamplesForTraining)
                _samples.Clear();
        }

        return null;
    }

    private readonly MahalanobisDetector _detector = new();
    private readonly List<Sample> _samples = new();
    private bool _isTrained = false;
}