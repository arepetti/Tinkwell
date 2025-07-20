namespace Tinkwell.Services.Proto.Proxies;

/// <summary>
/// Defines attributes for a measure.
/// </summary>
[Flags]
public enum StoreMeasureAttributes
{
    /// <summary>
    /// No attributes.
    /// </summary>
    None = 0,

    /// <summary>
    /// The measure is constant and cannot be changed after it has been set once.
    /// </summary>
    Constant = 1 << 0,

    /// <summary>
    /// The measure is derived from other measures.
    /// </summary>
    Derived = 1 << 1,

    /// <summary>
    /// The measure is generated automatically by the system.
    /// </summary>
    System = 1 << 2,
}
