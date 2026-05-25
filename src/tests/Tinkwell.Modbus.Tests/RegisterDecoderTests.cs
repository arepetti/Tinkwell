namespace Tinkwell.Modbus.Tests;

public class RegisterDecoderTests
{
    [Fact]
    public void Int16_NegativeTwosComplement()
    {
        Assert.Equal(-1.0, RegisterDecoder.Decode(new ushort[] { 0xFFFF }, ModbusDataType.Int16));
        Assert.Equal(-32768.0, RegisterDecoder.Decode(new ushort[] { 0x8000 }, ModbusDataType.Int16));
    }

    [Fact]
    public void UInt16_Identity()
    {
        Assert.Equal(65535.0, RegisterDecoder.Decode(new ushort[] { 0xFFFF }, ModbusDataType.UInt16));
    }

    [Fact]
    public void Int32BigEndian_WordOrder()
    {
        // 0x00010002 = 65538
        Assert.Equal(65538.0, RegisterDecoder.Decode(new ushort[] { 0x0001, 0x0002 }, ModbusDataType.Int32BigEndian));
    }

    [Fact]
    public void Int32LittleEndian_WordOrder()
    {
        // Low word first: [0x0002, 0x0001] → 0x00010002 = 65538
        Assert.Equal(65538.0, RegisterDecoder.Decode(new ushort[] { 0x0002, 0x0001 }, ModbusDataType.Int32LittleEndian));
    }

    [Fact]
    public void UInt32BigEndian_FullRange()
    {
        Assert.Equal((double)uint.MaxValue,
            RegisterDecoder.Decode(new ushort[] { 0xFFFF, 0xFFFF }, ModbusDataType.UInt32BigEndian));
    }

    [Fact]
    public void UInt32LittleEndian_WordOrder()
    {
        // 0xAAAABBBB, little-endian word order: [0xBBBB, 0xAAAA]
        Assert.Equal(0xAAAABBBBu,
            RegisterDecoder.Decode(new ushort[] { 0xBBBB, 0xAAAA }, ModbusDataType.UInt32LittleEndian));
    }

    [Fact]
    public void Float32BigEndian_KnownIeeeVector()
    {
        // 1.0f → IEEE 754 = 0x3F800000 → hi=0x3F80, lo=0x0000
        Assert.Equal(1.0, RegisterDecoder.Decode(new ushort[] { 0x3F80, 0x0000 }, ModbusDataType.Float32BigEndian), 6);
    }

    [Fact]
    public void Float32LittleEndian_KnownIeeeVector()
    {
        // 1.0f → 0x3F800000. Little-endian word order: lo=0x0000, hi=0x3F80
        Assert.Equal(1.0, RegisterDecoder.Decode(new ushort[] { 0x0000, 0x3F80 }, ModbusDataType.Float32LittleEndian), 6);
    }

    [Fact]
    public void Float32WordSwapped_KnownIeeeVector()
    {
        // BADC: words swapped from ABCD. 1.0f ABCD = [0x3F80, 0x0000];
        // swapped (BADC) = [0x0000, 0x3F80].
        Assert.Equal(1.0, RegisterDecoder.Decode(new ushort[] { 0x0000, 0x3F80 }, ModbusDataType.Float32WordSwapped), 6);
    }

    [Fact]
    public void Scale_IsApplied()
    {
        // 100 with scale 0.1 → 10.0 (typical temperature-tenths sensor)
        Assert.Equal(10.0, RegisterDecoder.Decode(new ushort[] { 100 }, ModbusDataType.UInt16, scale: 0.1), 6);
    }

    [Fact]
    public void RegisterCount_Is1ForSixteenBit_And2ForLarger()
    {
        Assert.Equal(1, RegisterDecoder.RegisterCount(ModbusDataType.Int16));
        Assert.Equal(1, RegisterDecoder.RegisterCount(ModbusDataType.UInt16));
        Assert.Equal(2, RegisterDecoder.RegisterCount(ModbusDataType.Int32BigEndian));
        Assert.Equal(2, RegisterDecoder.RegisterCount(ModbusDataType.UInt32LittleEndian));
        Assert.Equal(2, RegisterDecoder.RegisterCount(ModbusDataType.Float32BigEndian));
        Assert.Equal(2, RegisterDecoder.RegisterCount(ModbusDataType.Float32WordSwapped));
    }

    [Fact]
    public void UnknownDataType_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => RegisterDecoder.Decode(new ushort[] { 0 }, (ModbusDataType)999));
    }

    [Fact]
    public void Int32BigEndian_NegativeValue()
    {
        // 0xFFFF_8000 = -32768 (int32, hi=0xFFFF, lo=0x8000)
        var regs = new ushort[] { 0xFFFF, 0x8000 };
        Assert.Equal(-32768.0, RegisterDecoder.Decode(regs, ModbusDataType.Int32BigEndian), 0);
    }

    [Fact]
    public void Int32BigEndian_IntMinValue()
    {
        // 0x8000_0000 = int.MinValue: high word 0x8000, low 0x0000
        var regs = new ushort[] { 0x8000, 0x0000 };
        Assert.Equal(-2147483648.0, RegisterDecoder.Decode(regs, ModbusDataType.Int32BigEndian), 0);
    }

    [Fact]
    public void Float32BigEndian_ProducesNaN()
    {
        // IEEE 754: exp=all ones, non-zero significand
        // 0x7FC00000 is quiet NaN (common): hi=0x7FC0, lo=0x0000
        var f = RegisterDecoder.ToFloat32BigEndian(0x7FC0, 0x0000);
        Assert.True(float.IsNaN(f));
    }

    [Fact]
    public void Float32BigEndian_ProducesNegativeInfinity()
    {
        // 0xFF800000: negative infinity, hi=0xFF80, lo=0x0000
        var f = RegisterDecoder.ToFloat32BigEndian(0xFF80, 0x0000);
        Assert.True(float.IsNegativeInfinity(f));
    }

    [Fact]
    public void UInt32BigEndian_Full32Bit()
    {
        // (0x7FFF << 16) | 0xFFFE
        var regs = new ushort[] { 0x7FFF, 0xFFFE };
        Assert.Equal(0x7FFFFFFEu, RegisterDecoder.Decode(regs, ModbusDataType.UInt32BigEndian), 0);
    }

    [Fact]
    public void ToInt16_ZeroXffff_IsMinusOne()
    {
        Assert.Equal((short)-1, RegisterDecoder.ToInt16(0xFFFF));
    }
}
