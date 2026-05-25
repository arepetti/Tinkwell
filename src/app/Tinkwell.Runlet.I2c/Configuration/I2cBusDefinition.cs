namespace Tinkwell.Runlet.I2c.Configuration;

public sealed record I2cBusDefinition(
    string Name,
    int BusId,
    TimeSpan PollInterval,
    IReadOnlyList<I2cDeviceDefinition> Devices);
