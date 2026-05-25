using Tinkwell.Modbus;

namespace Tinkwell.Runlet.Modbus.Configuration;

/// <summary>
/// A <c>register</c> block inside a <c>device</c> block.
/// The block name defaults to the measure name.
/// </summary>
public sealed record ModbusRegisterDefinition(
    string Name,
    ushort Address,
    ModbusDataType DataType,
    ModbusRegisterKind RegisterKind,
    double Scale,
    string MeasureName);

/// <summary>Which Modbus function code to use for reading.</summary>
public enum ModbusRegisterKind
{
    /// <summary>Read Holding Registers (FC 03). Default.</summary>
    Holding,

    /// <summary>Read Input Registers (FC 04).</summary>
    Input,
}
