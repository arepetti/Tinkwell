using MathNet.Numerics.Distributions;
using MathNet.Numerics.LinearAlgebra;

namespace Tinkwell.Watchdog.AnomalyDetection;

sealed class MahalanobisDetector
{
    public bool Train(IEnumerable<Sample> samples)
    {
        var matrix = Matrix<double>.Build.Dense(samples.Count(), 4, (i, j) =>
        {
            var sample = samples.ElementAt(i);
            return j switch
            {
                0 => sample.Cpu,
                1 => sample.Memory,
                2 => sample.Threads,
                3 => sample.Handles,
                _ => 0
            };
        });

        _mean = matrix.ColumnSums() / samples.Count();
        var covariance = matrix.Covariance();
        if (!covariance.TryInvert(out _covarianceInverse))
        {
            if (!covariance.TikhonovRegularization().TryInvert(out _covarianceInverse))
                return false;
        }

        return true;
    }

    public bool IsAnomalous(Sample sample)
        => ComputeDistance(sample) > _threshold;

    private Vector<double>? _mean;
    private Matrix<double>? _covarianceInverse;
    private double _threshold = ChiSquared.InvCDF(4, 0.95); // 95%

    private double ComputeDistance(Sample sample)
    {
        var x = Vector<double>.Build.Dense([
            sample.Cpu,
            sample.Memory,
            sample.Threads,
            sample.Handles
        ]);

        var diff = x - _mean;
        var distanceSquared = diff * _covarianceInverse * diff;
        return Math.Sqrt(distanceSquared);
    }
}
