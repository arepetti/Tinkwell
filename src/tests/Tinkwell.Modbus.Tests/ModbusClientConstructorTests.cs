namespace Tinkwell.Modbus.Tests;

public class ModbusClientConstructorTests
{
    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void UnsynchronizedModbusTcpClient_InvalidHost_Throws(string host)
    {
        _ = Assert.Throws<ArgumentException>(() => new UnsynchronizedModbusTcpClient(host));
    }

    [Fact]
    public void UnsynchronizedModbusTcpClient_NullHost_Throws()
    {
        var ex = Assert.Throws<ArgumentNullException>(() => new UnsynchronizedModbusTcpClient(null!));
        Assert.Equal("host", ex.ParamName);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(65536)]
    public void UnsynchronizedModbusTcpClient_InvalidPort_Throws(int port)
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => new UnsynchronizedModbusTcpClient("127.0.0.1", port));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(502)]
    public void UnsynchronizedModbusTcpClient_Valid_DoesNotThrow(int port)
    {
        _ = new UnsynchronizedModbusTcpClient("127.0.0.1", port);
    }

    [Theory]
    [InlineData("   ")]
    public void ModbusRtuClient_InvalidPortName_Throws(string name)
    {
        _ = Assert.Throws<ArgumentException>(() => new ModbusRtuClient(name));
    }

    [Fact]
    public void ModbusRtuClient_NullPortName_Throws()
    {
        var ex = Assert.Throws<ArgumentNullException>(() => new ModbusRtuClient(null!));
        Assert.Equal("portName", ex.ParamName);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-19200)]
    public void ModbusRtuClient_InvalidBaud_Throws(int baud)
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(
            () => new ModbusRtuClient("COM1", baudRate: baud, readTimeoutMs: 1000));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ModbusRtuClient_InvalidReadTimeout_Throws(int readTimeout)
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(
            () => new ModbusRtuClient("COM1", readTimeoutMs: readTimeout));
    }

    [Fact]
    public void ModbusRtuClient_DefaultBaud_DoesNotThrow()
    {
        _ = new ModbusRtuClient("COM3");
    }
}
