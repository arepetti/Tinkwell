namespace Tinkwell.Runlet.Modbus.Configuration;

/// <summary>
/// A <c>device</c> block inside a <c>modbus</c> block — one Modbus slave.
/// </summary>
public sealed record ModbusDeviceDefinition(
    byte SlaveId,
    TimeSpan PollInterval,
    IReadOnlyList<ModbusRegisterDefinition> Registers);
