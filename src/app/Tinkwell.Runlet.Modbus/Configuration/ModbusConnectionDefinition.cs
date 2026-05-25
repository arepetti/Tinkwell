namespace Tinkwell.Runlet.Modbus.Configuration;

/// <summary>
/// A single <c>modbus</c> block — one physical connection (serial port or TCP endpoint).
/// </summary>
public sealed record ModbusConnectionDefinition(
    string Name,
    ModbusTransport Transport,
    string? Port,
    int BaudRate,
    string? Host,
    int TcpPort,
    IReadOnlyList<ModbusDeviceDefinition> Devices);

public enum ModbusTransport { Rtu, Tcp }
