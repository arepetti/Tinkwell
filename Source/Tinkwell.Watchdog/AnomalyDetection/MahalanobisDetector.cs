using MathNet.Numerics.Distributions;
using MathNet.Numerics.LinearAlgebra;

namespace Tinkwell.Watchdog.AnomalyDetection;

// https://insa.nic.in/writereaddata/UpLoadedFiles/PINSA/Vol02_1936_1_Art05.pdf
sealed class MahalanobisDetector
{
    // In plain English:
    //   * Mahalanobis distance measures how far a point is from the mean, scaled by covariance
    //   * Squared distances follow a chi-squared distribution under normality
    //   * You can interpret the threshold as: only 5% of normal points should exceed this distance
    // A few assumptions (to verify):
    //   * Data is multivariate normal
    //   * Not (too many) outliers during training
    //   * Data is not gaussian (well, shouldn't be)
    public double Threshold { get; set; }
        = ChiSquared.InvCDF(4, 0.95);

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
        => ComputeDistance(sample) > Threshold;

    private Vector<double>? _mean;
    private Matrix<double>? _covarianceInverse;

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
