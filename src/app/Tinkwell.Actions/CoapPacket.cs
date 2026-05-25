using System.Buffers.Binary;
using System.Text;

namespace Tinkwell.Actions.Coap;

internal static class CoapPacket
{
    public const byte MethodPost = 0x02;
    public const byte MethodPut = 0x03;
    public const byte MethodDelete = 0x04;

    public static byte[] Build(byte methodCode, string uriPath, string? payload)
    {
        var messageId = (ushort)Random.Shared.Next(0, 0xFFFF);
        byte[] token = [(byte)Random.Shared.Next(256), (byte)Random.Shared.Next(256)];

        using var ms = new MemoryStream();

        byte header = (byte)((1 << 6) | (0 << 4) | (token.Length & 0x0F));
        ms.WriteByte(header);
        ms.WriteByte(methodCode);

        Span<byte> idBytes = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(idBytes, messageId);
        ms.Write(idBytes);
        ms.Write(token);

        int prevOptionNum = 0;
        foreach (var seg in uriPath.Split('/', StringSplitOptions.RemoveEmptyEntries))
            WriteOption(ms, 11, ref prevOptionNum, Encoding.UTF8.GetBytes(seg));

        if (!string.IsNullOrEmpty(payload))
        {
            ms.WriteByte(0xFF);
            ms.Write(Encoding.UTF8.GetBytes(payload));
        }

        return ms.ToArray();
    }

    public static (int Class, int Detail) ParseResponseCode(byte[] data)
    {
        if (data.Length < 2)
            return (5, 0);

        byte code = data[1];
        return (code >> 5, code & 0x1F);
    }

    private static void WriteOption(MemoryStream ms, int number, ref int prevNumber, byte[] value)
    {
        int delta = number - prevNumber;
        prevNumber = number;
        int length = value.Length;

        int deltaNibble = delta < 13 ? delta : delta < 269 ? 13 : 14;
        int lengthNibble = length < 13 ? length : length < 269 ? 13 : 14;

        ms.WriteByte((byte)((deltaNibble << 4) | lengthNibble));

        if (delta >= 13 && delta < 269)
            ms.WriteByte((byte)(delta - 13));
        else if (delta >= 269)
        {
            Span<byte> b = stackalloc byte[2];
            BinaryPrimitives.WriteUInt16BigEndian(b, (ushort)(delta - 269));
            ms.Write(b);
        }

        if (length >= 13 && length < 269)
            ms.WriteByte((byte)(length - 13));
        else if (length >= 269)
        {
            Span<byte> b = stackalloc byte[2];
            BinaryPrimitives.WriteUInt16BigEndian(b, (ushort)(length - 269));
            ms.Write(b);
        }

        ms.Write(value);
    }
}
