namespace Tinkwell.Runlet.I2c.Configuration;

public sealed record I2cConfig(IReadOnlyList<I2cBusDefinition> Buses);
