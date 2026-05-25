using Tinkwell.Runlet.TextQuery.Configuration;
using Tinkwell.Runlet.TextQuery;

namespace Tinkwell.Runlet.TextQuery.Tests;

public class CreateTransportValidationTests
{
    private static IReadOnlyList<TextQueryReadDefinition> NoReads { get; } = [];

    [Fact]
    public void CreateTransport_Tcp_MissingHost_ThrowsInvalidOperation()
    {
        var source = new TextQuerySourceDefinition(
            Name: "t",
            Transport: TextQueryTransport.Tcp,
            Host: null,
            TcpPort: 5000,
            SerialPort: null,
            BaudRate: 9600,
            FilePath: null,
            Command: null,
            LineTerminator: "\n",
            ReadTimeoutMs: 1000,
            PollInterval: TimeSpan.FromSeconds(1),
            Reads: NoReads);

        var ex = Assert.Throws<InvalidOperationException>(() => TextQueryPollingManager.CreateTransport(source));
        Assert.Contains("host", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CreateTransport_Serial_MissingPortName_ThrowsInvalidOperation()
    {
        var source = new TextQuerySourceDefinition(
            Name: "s",
            Transport: TextQueryTransport.Serial,
            Host: null,
            TcpPort: 0,
            SerialPort: null,
            BaudRate: 9600,
            FilePath: null,
            Command: null,
            LineTerminator: "\n",
            ReadTimeoutMs: 1000,
            PollInterval: TimeSpan.FromSeconds(1),
            Reads: NoReads);

        var ex = Assert.Throws<InvalidOperationException>(() => TextQueryPollingManager.CreateTransport(source));
        Assert.Contains("serial-port", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CreateTransport_File_MissingPath_ThrowsInvalidOperation()
    {
        var source = new TextQuerySourceDefinition(
            Name: "f",
            Transport: TextQueryTransport.File,
            Host: null,
            TcpPort: 0,
            SerialPort: null,
            BaudRate: 9600,
            FilePath: null,
            Command: null,
            LineTerminator: "\n",
            ReadTimeoutMs: 1000,
            PollInterval: TimeSpan.FromSeconds(1),
            Reads: NoReads);

        var ex = Assert.Throws<InvalidOperationException>(() => TextQueryPollingManager.CreateTransport(source));
        Assert.Contains("path", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CreateTransport_Command_MissingCommand_ThrowsInvalidOperation()
    {
        var source = new TextQuerySourceDefinition(
            Name: "c",
            Transport: TextQueryTransport.Command,
            Host: null,
            TcpPort: 0,
            SerialPort: null,
            BaudRate: 9600,
            FilePath: null,
            Command: null,
            LineTerminator: "\n",
            ReadTimeoutMs: 1000,
            PollInterval: TimeSpan.FromSeconds(1),
            Reads: NoReads);

        var ex = Assert.Throws<InvalidOperationException>(() => TextQueryPollingManager.CreateTransport(source));
        Assert.Contains("command", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
