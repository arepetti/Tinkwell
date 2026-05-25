namespace Tinkwell.Runlet.I2c.Configuration;

public sealed record I2cReadDefinition(
    string Name,
    byte Register,
    int Length,
    I2cDataType DataType,
    double Scale,
    string MeasureName);
