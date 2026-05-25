namespace Tinkwell.Runlet.I2c.Configuration;

public sealed record I2cDeviceDefinition(
    int Address,
    IReadOnlyList<I2cReadDefinition> Reads);
