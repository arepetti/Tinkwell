namespace Tinkwell.Modbus.Tests;

/// <summary>
/// Exposes <see cref="ModbusClientBase"/> protected static helpers for direct unit tests.
/// </summary>
public sealed class ModbusClientBaseValidationProxy : ModbusClientBase
{
    public static void ValidateCountEx(ushort count, string paramName) => ValidateCount(count, paramName);

    public static void ValidateUnicastSlaveIdEx(byte slaveId, string paramName) =>
        ValidateUnicastSlaveId(slaveId, paramName);

    public static void ValidateHostOrPortNameEx(string? name, string paramName) =>
        ValidateHostOrPortName(name!, paramName);

    public static void ValidateReadTimeoutMsEx(int readTimeoutMs, string paramName) =>
        ValidateReadTimeoutMs(readTimeoutMs, paramName);

    public static void ValidateTcpPortEx(int port, string paramName) => ValidateTcpPort(port, paramName);

    public static void ValidateBaudRateEx(int baudRate, string paramName) => ValidateBaudRate(baudRate, paramName);

    public static void ValidateResponseEx(ReadOnlySpan<byte> pdu, byte expectedFunctionCode) =>
        ValidateResponse(pdu, expectedFunctionCode);

    public static ushort[] DecodeHoldingOrInputRegistersEx(
        ReadOnlySpan<byte> responsePdu,
        byte functionCode,
        int requestedCount) => DecodeHoldingOrInputRegisters(responsePdu, functionCode, requestedCount);

    public static string ExceptionMessageEx(byte code) => ExceptionMessage(code);

    protected override bool IsModbusTcpTransport => false;

    public override bool IsConnected => false;

    public override Task ConnectAsync(CancellationToken ct = default) => Task.CompletedTask;

    public override ValueTask DisposeAsync() => default;

    protected override bool IsDisposed => true;

    protected override Task<byte[]> SendRequestAsync(byte slaveId, byte[] pdu, CancellationToken ct) =>
        throw new NotSupportedException("Test proxy only; use for static validation helpers.");
}
