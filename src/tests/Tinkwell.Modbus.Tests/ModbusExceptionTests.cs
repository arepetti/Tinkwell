namespace Tinkwell.Modbus.Tests;

public class ModbusExceptionTests
{
    [Fact]
    public void Constructor_NullMessage_UsesDefault()
    {
        var ex = new ModbusException(null);
        Assert.Equal("A Modbus error occurred.", ex.Message);
    }

    [Fact]
    public void Constructor_WithMessage_PreservesMessage()
    {
        const string msg = "Device rejected the function.";
        var ex = new ModbusException(msg);
        Assert.Equal(msg, ex.Message);
    }

    [Fact]
    public void Constructor_WithMessageAndInner_PreservesInner()
    {
        var inner = new IOException("I/O");
        const string msg = "wrapper";
        var ex = new ModbusException(msg, inner);
        Assert.Same(inner, ex.InnerException);
    }

    [Fact]
    public void ExceptionCode_DefaultsToNull()
    {
        var ex = new ModbusException("e");
        Assert.Null(ex.ExceptionCode);
    }
}
