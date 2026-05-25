namespace Tinkwell.Runlet.TextQuery.Configuration;

/// <summary>
/// A single <c>query</c> block — one data source (TCP socket, serial port, file, or command).
/// </summary>
public sealed record TextQuerySourceDefinition(
    string Name,
    TextQueryTransport Transport,
    string? Host,
    int TcpPort,
    string? SerialPort,
    int BaudRate,
    string? FilePath,
    string? Command,
    string LineTerminator,
    int ReadTimeoutMs,
    TimeSpan PollInterval,
    IReadOnlyList<TextQueryReadDefinition> Reads);

public enum TextQueryTransport { Tcp, Serial, File, Command }
