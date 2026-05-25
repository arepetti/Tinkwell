namespace Tinkwell.Measures;

/// <summary>
/// Exception for measure-related domain errors (validation failures,
/// type mismatches, constant protection violations, etc.).
/// </summary>
public sealed class MeasureException : TinkwellException
{
    public MeasureException(string message) : base(message) { }
    public MeasureException(string message, Exception? innerException) : base(message, innerException) { }
}
