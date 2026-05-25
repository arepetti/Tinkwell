using System.IO.Ports;
using System.Text;

namespace Tinkwell.Runlet.TextQuery.Transports;

internal sealed class SerialTextTransport : ITextTransport
{
    private readonly string _portName;
    private readonly int _baudRate;
    private SerialPort? _port;

    public SerialTextTransport(string portName, int baudRate)
    {
        _portName = portName;
        _baudRate = baudRate;
    }

    public Task ConnectAsync(CancellationToken ct)
    {
        _port = new SerialPort(_portName, _baudRate)
        {
            ReadTimeout = 2000,
            WriteTimeout = 2000,
            Encoding = Encoding.ASCII,
        };
        _port.Open();
        return Task.CompletedTask;
    }

    public Task<string> QueryAsync(string? command, string lineTerminator, int timeoutMs, CancellationToken ct)
    {
        if (_port is null || !_port.IsOpen)
            throw new InvalidOperationException("Not connected");

        _port.ReadTimeout = timeoutMs;

        if (command is not null)
            _port.Write(command + lineTerminator);

        try
        {
            var line = _port.ReadLine();
            return Task.FromResult(line.Trim());
        }
        catch (TimeoutException)
        {
            return Task.FromResult(string.Empty);
        }
    }

    public ValueTask DisposeAsync()
    {
        _port?.Close();
        _port?.Dispose();
        return ValueTask.CompletedTask;
    }
}
