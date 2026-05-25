namespace Tinkwell.Runlet.TextQuery.Configuration;

/// <summary>
/// A <c>read</c> block inside a <c>query</c> block — one value to extract.
/// </summary>
public sealed record TextQueryReadDefinition(
    string Name,
    string? SendCommand,
    string Pattern,
    int CaptureGroup,
    double Scale,
    string MeasureName);
