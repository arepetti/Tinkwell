namespace Tinkwell.Runlet.Modbus.Configuration;

/// <summary>
/// Root configuration produced by <see cref="ModbusConfigParser"/>.
/// </summary>
public sealed record ModbusConfig(IReadOnlyList<ModbusConnectionDefinition> Connections);
