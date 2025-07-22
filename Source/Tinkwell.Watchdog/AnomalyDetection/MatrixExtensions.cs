using MathNet.Numerics.LinearAlgebra;

namespace Tinkwell.Watchdog.AnomalyDetection;

static class MatrixExtensions
{
    public static bool TryInvert(this Matrix<double> matrix, out Matrix<double> inverse)
    {
        inverse = matrix;

        if (matrix.IsMaybeSingularFast())
            return false;

        try
        {
            inverse = matrix.Inverse();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static bool IsMaybeSingularFast(this Matrix<double> matrix)
    {
        double determinant = matrix.Determinant();
        return Math.Abs(determinant) < 1e-10;
    }

    public static Matrix<double> Covariance(this Matrix<double> matrix)
    {
        var mean = matrix.ColumnSums() / matrix.RowCount;
        var centered = Matrix<double>.Build.Dense(matrix.RowCount, matrix.ColumnCount);

        for (int i = 0; i < matrix.RowCount; i++)
        {
            var row = matrix.Row(i);
            var centeredRow = row - mean;
            centered.SetRow(i, centeredRow);
        }

        return centered.TransposeThisAndMultiply(centered) / (matrix.RowCount - 1);
    }

    public static Matrix<double> TikhonovRegularization(this Matrix<double> covariance)
    {
        var identity = Matrix<double>.Build.DenseIdentity(4);
        return covariance + identity * 1e-3;
    }
}
