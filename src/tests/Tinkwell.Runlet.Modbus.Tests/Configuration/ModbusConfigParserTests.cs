using Tinkwell.Configuration;
using Tinkwell.Configuration.Parser;
using Tinkwell.Modbus;
using Tinkwell.Runlet.Modbus.Configuration;

namespace Tinkwell.Runlet.Modbus.Configuration.Tests;

public class ModbusConfigParserTests
{
    private readonly ModbusConfigParser _parser = new();

    private Task<ModbusConfig> ParseFile(string relativePath)
    {
        var path = Path.Combine("TestFiles", relativePath);
        return _parser.LoadFileAsync(path);
    }

    [Fact]
    public async Task BasicRtu_ParsesConnectionDeviceAndRegisters()
    {
        var config = await ParseFile("basic-rtu.tw");

        var conn = Assert.Single(config.Connections);
        Assert.Equal("plc1", conn.Name);
        Assert.Equal(ModbusTransport.Rtu, conn.Transport);
        Assert.Equal("/dev/ttyUSB0", conn.Port);
        Assert.Equal(19200, conn.BaudRate);

        var device = Assert.Single(conn.Devices);
        Assert.Equal(1, device.SlaveId);
        Assert.Equal(TimeSpan.FromMilliseconds(500), device.PollInterval);

        Assert.Equal(2, device.Registers.Count);

        var temp = device.Registers[0];
        Assert.Equal("temperature", temp.Name);
        Assert.Equal(0x0010, temp.Address);
        Assert.Equal(ModbusDataType.Int16, temp.DataType);
        Assert.Equal(ModbusRegisterKind.Input, temp.RegisterKind);
        Assert.Equal(0.1, temp.Scale);
        Assert.Equal("reactor-temp", temp.MeasureName);

        var voltage = device.Registers[1];
        Assert.Equal(32, voltage.Address);
        Assert.Equal(ModbusDataType.Float32BigEndian, voltage.DataType);
        Assert.Equal(ModbusRegisterKind.Holding, voltage.RegisterKind);
        Assert.Equal(1.0, voltage.Scale);
        Assert.Equal("voltage", voltage.MeasureName);
    }

    [Fact]
    public async Task BasicTcp_ParsesHostAndPort()
    {
        var config = await ParseFile("basic-tcp.tw");

        var conn = Assert.Single(config.Connections);
        Assert.Equal(ModbusTransport.Tcp, conn.Transport);
        Assert.Equal("192.168.1.100", conn.Host);
        Assert.Equal(5020, conn.TcpPort);

        var register = Assert.Single(conn.Devices[0].Registers);
        Assert.Equal(ModbusDataType.UInt16, register.DataType);
    }

    [Fact]
    public async Task InvalidTransport_Throws()
    {
        var ex = await Assert.ThrowsAsync<ConfigurationSyntaxException>(
            () => ParseFile("invalid-transport.tw"));

        Assert.Contains("zigbee", ex.Message);
    }

    [Fact]
    public async Task InvalidSlaveId_Throws()
    {
        var ex = await Assert.ThrowsAsync<ConfigurationSyntaxException>(
            () => ParseFile("invalid-slave-id.tw"));

        Assert.Contains("slave", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task InvalidDataType_Throws()
    {
        var ex = await Assert.ThrowsAsync<ConfigurationSyntaxException>(
            () => ParseFile("invalid-data-type.tw"));

        Assert.Contains("int99-be", ex.Message);
    }

    [Fact]
    public async Task DataTypeAliases_AllResolve()
    {
        var config = await ParseFile("data-type-aliases.tw");
        var registers = config.Connections[0].Devices[0].Registers;

        Assert.Equal(ModbusDataType.Int16, registers[0].DataType);
        Assert.Equal(ModbusDataType.UInt16, registers[1].DataType);
        Assert.Equal(ModbusDataType.Int32BigEndian, registers[2].DataType);
        Assert.Equal(ModbusDataType.Int32BigEndian, registers[3].DataType);
        Assert.Equal(ModbusDataType.Int32LittleEndian, registers[4].DataType);
        Assert.Equal(ModbusDataType.UInt32BigEndian, registers[5].DataType);
        Assert.Equal(ModbusDataType.UInt32LittleEndian, registers[6].DataType);
        Assert.Equal(ModbusDataType.Float32BigEndian, registers[7].DataType);
        Assert.Equal(ModbusDataType.Float32BigEndian, registers[8].DataType);
        Assert.Equal(ModbusDataType.Float32BigEndian, registers[9].DataType);
        Assert.Equal(ModbusDataType.Float32LittleEndian, registers[10].DataType);
        Assert.Equal(ModbusDataType.Float32WordSwapped, registers[11].DataType);
    }
}
