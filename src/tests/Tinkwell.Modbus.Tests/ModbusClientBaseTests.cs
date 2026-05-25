using System.Buffers.Binary;

namespace Tinkwell.Modbus.Tests;

public class ModbusClientBaseTests
{
    #region ValidateCount

    [Theory]
    [InlineData(0)]
    [InlineData(126)]
    public void ValidateCount_OutOfRange_Throws(ushort count)
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(
            () => ModbusClientBaseValidationProxy.ValidateCountEx(count, nameof(count)));
        Assert.Equal(nameof(count), ex.ParamName);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(125)]
    public void ValidateCount_InRange_DoesNotThrow(ushort count)
    {
        ModbusClientBaseValidationProxy.ValidateCountEx(count, nameof(count));
    }

    #endregion

    #region ValidateUnicastSlaveId

    [Theory]
    [InlineData(0)]
    [InlineData(248)]
    [InlineData(255)]
    public void ValidateUnicastSlaveId_Invalid_Throws(byte slaveId)
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(
            () => ModbusClientBaseValidationProxy.ValidateUnicastSlaveIdEx(slaveId, nameof(slaveId)));
        Assert.Equal(nameof(slaveId), ex.ParamName);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(247)]
    public void ValidateUnicastSlaveId_Valid_DoesNotThrow(byte slaveId)
    {
        ModbusClientBaseValidationProxy.ValidateUnicastSlaveIdEx(slaveId, nameof(slaveId));
    }

    #endregion

    #region ValidateHostOrPortName

    [Fact]
    public void ValidateHostOrPortName_Null_Throws()
    {
        var ex = Assert.Throws<ArgumentNullException>(
            () => ModbusClientBaseValidationProxy.ValidateHostOrPortNameEx(null, "portName"));
        Assert.Equal("portName", ex.ParamName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ValidateHostOrPortName_EmptyOrWhitespace_Throws(string? name)
    {
        Assert.Throws<ArgumentException>(
            () => ModbusClientBaseValidationProxy.ValidateHostOrPortNameEx(name, "portName"));
    }

    [Fact]
    public void ValidateHostOrPortName_NonEmpty_DoesNotThrow()
    {
        ModbusClientBaseValidationProxy.ValidateHostOrPortNameEx("COM1", "portName");
    }

    #endregion

    #region ValidateReadTimeoutMs

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ValidateReadTimeoutMs_NotPositive_Throws(int ms)
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(
            () => ModbusClientBaseValidationProxy.ValidateReadTimeoutMsEx(ms, nameof(ms)));
        Assert.Equal(nameof(ms), ex.ParamName);
    }

    [Fact]
    public void ValidateReadTimeoutMs_Positive_DoesNotThrow()
    {
        ModbusClientBaseValidationProxy.ValidateReadTimeoutMsEx(1, "readTimeoutMs");
    }

    #endregion

    #region ValidateTcpPort

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(65536)]
    public void ValidateTcpPort_OutOfRange_Throws(int port)
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(
            () => ModbusClientBaseValidationProxy.ValidateTcpPortEx(port, nameof(port)));
        Assert.Equal(nameof(port), ex.ParamName);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(502)]
    [InlineData(65535)]
    public void ValidateTcpPort_InRange_DoesNotThrow(int port)
    {
        ModbusClientBaseValidationProxy.ValidateTcpPortEx(port, nameof(port));
    }

    #endregion

    #region ValidateBaudRate

    [Theory]
    [InlineData(0)]
    [InlineData(-9600)]
    public void ValidateBaudRate_NotPositive_Throws(int baud)
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(
            () => ModbusClientBaseValidationProxy.ValidateBaudRateEx(baud, nameof(baud)));
        Assert.Equal(nameof(baud), ex.ParamName);
    }

    [Fact]
    public void ValidateBaudRate_Positive_DoesNotThrow()
    {
        ModbusClientBaseValidationProxy.ValidateBaudRateEx(9600, "baudRate");
    }

    #endregion

    #region ValidateResponse

    [Fact]
    public void ValidateResponse_Empty_Throws()
    {
        var ex = Assert.Throws<ModbusException>(
            () => ModbusClientBaseValidationProxy.ValidateResponseEx(ReadOnlySpan<byte>.Empty, 0x03));
        Assert.Equal("Empty response PDU", ex.Message);
    }

    [Theory]
    [InlineData(0x01, 0x80 | 0x03, "Illegal Function")]
    [InlineData(0x02, 0x80 | 0x03, "Illegal Data Address")]
    [InlineData(0, 0x80 | 0x04, "Unknown (0x00)")]
    public void ValidateResponse_ExceptionPdu_ThrowsWithExceptionCode(
        byte exCode,
        byte firstByte,
        string expectedPhrase)
    {
        var pdu = new[] { firstByte, exCode };
        var ex = Assert.Throws<ModbusException>(
            () => ModbusClientBaseValidationProxy.ValidateResponseEx(pdu, 0x03));
        Assert.Equal(exCode, ex.ExceptionCode);
        Assert.Contains(expectedPhrase, ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateResponse_ExceptionPdu_OneByte_UsesExceptionCodeZero()
    {
        var pdu = new[] { (byte)(0x80 | 0x03) };
        var ex = Assert.Throws<ModbusException>(
            () => ModbusClientBaseValidationProxy.ValidateResponseEx(pdu, 0x03));
        Assert.Equal((byte)0, ex.ExceptionCode);
    }

    [Fact]
    public void ValidateResponse_FunctionCodeMismatch_Throws()
    {
        var pdu = new byte[] { 0x04 };
        var ex = Assert.Throws<ModbusException>(
            () => ModbusClientBaseValidationProxy.ValidateResponseEx(pdu, 0x03));
        Assert.Contains("Unexpected function code", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateResponse_Matching_DoesNotThrow()
    {
        var pdu = new byte[] { 0x03, 0x02, 0x00, 0x01 };
        ModbusClientBaseValidationProxy.ValidateResponseEx(pdu, 0x03);
    }

    #endregion

    #region DecodeHoldingOrInputRegisters

    [Fact]
    public void DecodeHoldingOrInputRegisters_Valid_ThreeRegisters()
    {
        // FC 0x03, byte count 6, data for three registers
        var data = new byte[] { 0x03, 0x06, 0x00, 0x0A, 0x00, 0x0B, 0x00, 0x0C };
        var regs = ModbusClientBaseValidationProxy.DecodeHoldingOrInputRegistersEx(
            data, 0x03, requestedCount: 3);
        Assert.Equal(3, regs.Length);
        Assert.Equal(10, regs[0]);
        Assert.Equal(11, regs[1]);
        Assert.Equal(12, regs[2]);
    }

    [Fact]
    public void DecodeHoldingOrInputRegisters_ByteCountNotEven_Throws()
    {
        var data = new byte[] { 0x03, 0x01, 0x00 };
        var ex = Assert.Throws<ModbusException>(
            () => ModbusClientBaseValidationProxy.DecodeHoldingOrInputRegistersEx(data, 0x03, 1));
        Assert.Contains("must be even", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DecodeHoldingOrInputRegisters_ByteCountMismatch_Throws()
    {
        var data = new byte[] { 0x03, 0x04, 0, 0, 0, 0 };
        var ex = Assert.Throws<ModbusException>(
            () => ModbusClientBaseValidationProxy.DecodeHoldingOrInputRegistersEx(data, 0x03, 3));
        Assert.Contains("Invalid byte count", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DecodeHoldingOrInputRegisters_PduShorterThanDeclaredData_Throws()
    {
        // byte count says 4 but only 1 data byte after header
        var data = new byte[] { 0x03, 0x04, 0x00, 0x01 };
        var ex = Assert.Throws<ModbusException>(
            () => ModbusClientBaseValidationProxy.DecodeHoldingOrInputRegistersEx(data, 0x03, 2));
        Assert.Contains("too short", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DecodeHoldingOrInputRegisters_OnlyFunctionCode_Throws()
    {
        var data = new byte[] { 0x03 };
        var ex = Assert.Throws<ModbusException>(
            () => ModbusClientBaseValidationProxy.DecodeHoldingOrInputRegistersEx(data, 0x03, 1));
        Assert.Contains("missing byte count", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DecodeHoldingOrInputRegisters_ExceptionInPayload_StillFailsValidateResponse()
    {
        var data = new byte[] { 0x83, 0x02 };
        Assert.Throws<ModbusException>(
            () => ModbusClientBaseValidationProxy.DecodeHoldingOrInputRegistersEx(data, 0x03, 1));
    }

    #endregion

    #region ExceptionMessage (via proxy)

    [Theory]
    [InlineData(0x01, "Illegal Function")]
    [InlineData(0x04, "Slave Device Failure")]
    [InlineData(0x0A, "Gateway Path Unavailable")]
    [InlineData(0x3F, "Unknown (0x3F)")]
    public void ExceptionMessage_MapsStandardAndUnknown_Codes(byte code, string contains)
    {
        var s = ModbusClientBaseValidationProxy.ExceptionMessageEx(code);
        Assert.Contains(contains, s, StringComparison.Ordinal);
    }

    #endregion

    #region WriteSingleRegister echo (indirect, private static path)

    [Fact]
    public async Task WriteSingleRegisterAsync_EchoMatches_Completes()
    {
        var (addr, value) = ((ushort)0x1234, (ushort)0xABCD);
        var echo = BuildFc06Response(addr, value);
        await using var client = new FakeModbusClient { TcpTransport = true, NextResponse = echo };
        await client.ConnectAsync();
        await client.WriteSingleRegisterAsync(1, addr, value);
    }

    [Fact]
    public async Task WriteSingleRegisterAsync_EchoAddressMismatch_Throws()
    {
        var echo = BuildFc06Response(0x0001, 0xABCD);
        await using var client = new FakeModbusClient { TcpTransport = true, NextResponse = echo };
        await client.ConnectAsync();
        var ex = await Assert.ThrowsAsync<ModbusException>(
            () => client.WriteSingleRegisterAsync(1, 0x0002, 0xABCD));
        Assert.Contains("echo mismatch", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WriteSingleRegisterAsync_EchoTooShort_Throws()
    {
        var echo = new byte[] { 0x06, 0x00, 0x01 };
        await using var client = new FakeModbusClient { TcpTransport = true, NextResponse = echo };
        await client.ConnectAsync();
        var ex = await Assert.ThrowsAsync<ModbusException>(
            () => client.WriteSingleRegisterAsync(1, 0x0001, 0x0001));
        Assert.Contains("too short", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WriteSingleRegisterAsync_DeviceException_Throws()
    {
        var bad = new byte[] { 0x86, 0x02 };
        await using var client = new FakeModbusClient { TcpTransport = true, NextResponse = bad };
        await client.ConnectAsync();
        var ex = await Assert.ThrowsAsync<ModbusException>(
            () => client.WriteSingleRegisterAsync(1, 0, 0));
        Assert.Equal((byte)0x02, ex.ExceptionCode);
    }

    #endregion

    #region Broadcast and TCP vs RTU (fake transport)

    [Fact]
    public async Task ReadHoldingRegistersAsync_Unit0_OnTcp_Throws()
    {
        await using var client = new FakeModbusClient { TcpTransport = true, NextResponse = [] };
        await client.ConnectAsync();
        await Assert.ThrowsAsync<NotSupportedException>(
            () => client.ReadHoldingRegistersAsync(0, 0, 1));
    }

    [Fact]
    public async Task ReadInputRegistersAsync_Unit0_Throws()
    {
        await using var client = new FakeModbusClient { TcpTransport = true, NextResponse = [] };
        await client.ConnectAsync();
        var ex = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => client.ReadInputRegistersAsync(0, 0, 1));
        Assert.Equal("slaveId", ex.ParamName);
    }

    [Fact]
    public async Task WriteSingleRegisterAsync_Unit0_OnTcp_Throws()
    {
        await using var client = new FakeModbusClient { TcpTransport = true, NextResponse = [] };
        await client.ConnectAsync();
        await Assert.ThrowsAsync<NotSupportedException>(
            () => client.WriteSingleRegisterAsync(0, 0, 0));
    }

    [Fact]
    public async Task ReadHoldingRegistersAsync_Unit0_Rtu_UsesBroadcastOverride()
    {
        await using var client = new FakeRtuBroadcastClient { NextResponse = [0x03, 0x02, 0x00, 0x00] };
        await client.ConnectAsync();
        var r = await client.ReadHoldingRegistersAsync(0, 0x1000, 10);
        Assert.Equal(10, r.Length);
        Assert.All(r, v => Assert.Equal(0, v));
    }

    [Fact]
    public async Task WriteSingleRegisterAsync_Unit0_Rtu_CallsNoResponseWrite()
    {
        var inner = new FakeRtuBroadcastClient { NextResponse = [] };
        await using var client = inner;
        await client.ConnectAsync();
        await client.WriteSingleRegisterAsync(0, 0x2000, 0x00AB);
        Assert.NotNull(inner.LastWriteAddress);
        Assert.NotNull(inner.LastWriteValue);
        Assert.Equal((ushort)0x2000, inner.LastWriteAddress);
        Assert.Equal((ushort)0x00AB, inner.LastWriteValue);
    }

    [Fact]
    public async Task ReadHoldingRegistersBroadcastRtu_NonOverriddenRtu_Throws()
    {
        await using var client = new FakeRtuWithoutBroadcast();
        await client.ConnectAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.ReadHoldingRegistersAsync(0, 0, 1));
    }

    [Fact]
    public async Task WriteBroadcastRtu_NonOverriddenRtu_Throws()
    {
        await using var client = new FakeRtuWithoutBroadcast();
        await client.ConnectAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.WriteSingleRegisterAsync(0, 0, 0));
    }

    #endregion

    #region Helpers

    private static byte[] BuildFc06Response(ushort address, ushort value)
    {
        var p = new byte[5];
        p[0] = 0x06;
        BinaryPrimitives.WriteUInt16BigEndian(p.AsSpan(1), address);
        BinaryPrimitives.WriteUInt16BigEndian(p.AsSpan(3), value);
        return p;
    }

    private sealed class FakeModbusClient : ModbusClientBase
    {
        public bool TcpTransport { get; init; } = true;
        protected override bool IsModbusTcpTransport => TcpTransport;
        public byte[]? NextResponse { get; init; }

        private bool _disposed;
        public bool Connected = true;
        public override bool IsConnected => !_disposed && Connected;
        protected override bool IsDisposed => _disposed;
        public override Task ConnectAsync(CancellationToken ct = default) => Task.CompletedTask;
        public override ValueTask DisposeAsync()
        {
            _disposed = true;
            return default;
        }

        protected override Task<byte[]> SendRequestAsync(
            byte slaveId,
            byte[] pdu,
            CancellationToken ct) =>
            Task.FromResult(NextResponse ?? throw new InvalidOperationException("Set NextResponse for unicast path."));
    }

    private sealed class FakeRtuBroadcastClient : ModbusClientBase
    {
        public byte[]? NextResponse { get; set; } = [0x03, 0x02, 0x00, 0x00];
        public ushort? LastWriteAddress;
        public ushort? LastWriteValue;

        protected override bool IsModbusTcpTransport => false;
        private bool _disposed;
        public bool Connected = true;
        public override bool IsConnected => !_disposed && Connected;
        protected override bool IsDisposed => _disposed;
        public override Task ConnectAsync(CancellationToken ct = default) => Task.CompletedTask;
        public override ValueTask DisposeAsync()
        {
            _disposed = true;
            return default;
        }

        protected override Task<ushort[]> ReadHoldingRegistersBroadcastRtuAsync(
            ushort startAddress,
            ushort count,
            CancellationToken ct) =>
            Task.FromResult(new ushort[count]);

        protected override Task WriteBroadcastPduRtuNoResponseAsync(byte[] pdu, CancellationToken ct)
        {
            if (pdu[0] != 0x06)
            {
                throw new InvalidOperationException("Expected FC 0x06 broadcast");
            }

            LastWriteAddress = BinaryPrimitives.ReadUInt16BigEndian(pdu.AsSpan(1));
            LastWriteValue = BinaryPrimitives.ReadUInt16BigEndian(pdu.AsSpan(3));
            return Task.CompletedTask;
        }

        protected override Task<byte[]> SendRequestAsync(
            byte slaveId,
            byte[] pdu,
            CancellationToken ct) =>
            Task.FromResult(NextResponse ?? throw new InvalidOperationException("Set NextResponse for unicast."));
    }

    private sealed class FakeRtuWithoutBroadcast : ModbusClientBase
    {
        protected override bool IsModbusTcpTransport => false;
        private bool _disposed;
        public override bool IsConnected => !_disposed;
        protected override bool IsDisposed => _disposed;
        public override Task ConnectAsync(CancellationToken ct = default) => Task.CompletedTask;
        public override ValueTask DisposeAsync()
        {
            _disposed = true;
            return default;
        }

        protected override Task<byte[]> SendRequestAsync(
            byte slaveId,
            byte[] pdu,
            CancellationToken ct) =>
            Task.FromResult(pdu);
    }

    #endregion
}
