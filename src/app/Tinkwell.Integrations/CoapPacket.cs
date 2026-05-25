using System.Buffers.Binary;
using System.Text;

namespace Tinkwell.Integration;

internal static class CoapPacket
{
    public const byte MethodPost = 0x02;
    public const byte MethodPut = 0x03;
    public const byte MethodDelete = 0x04;

    private const int Version1 = 1;
    private const int TypeConfirmable = 0;
    private const int TokenLength = 2;

    private const int OptionUriPath = 11;
    private const byte PayloadMarker = 0xFF;

    private const int ResponseCodeOffset = 1;
    private const int ResponseClassBits = 5;
    private const int ResponseDetailMask = 0x1F;
    private const int ServerErrorClass = 5;

    /// <summary>RFC 7252 extended encoding thresholds.</summary>
    private const int ExtendedOneByte = 13;
    private const int ExtendedTwoBytes = 269;

    public static byte[] Build(byte methodCode, string uriPath, string? payload)
    {
        var messageId = (ushort)Random.Shared.Next(0, 0xFFFF);
        byte[] token = [(byte)Random.Shared.Next(256), (byte)Random.Shared.Next(256)];

        using var ms = new MemoryStream();

        byte header = (byte)((Version1 << 6) | (TypeConfirmable << 4) | (token.Length & 0x0F));
        ms.WriteByte(header);
        ms.WriteByte(methodCode);

        Span<byte> idBytes = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(idBytes, messageId);
        ms.Write(idBytes);
        ms.Write(token);

        int prevOptionNum = 0;
        foreach (var seg in uriPath.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            WriteOption(ms, OptionUriPath, ref prevOptionNum, Encoding.UTF8.GetBytes(seg));
        }

        if (!string.IsNullOrEmpty(payload))
        {
            ms.WriteByte(PayloadMarker);
            ms.Write(Encoding.UTF8.GetBytes(payload));
        }

        return ms.ToArray();
    }

    public static (int Class, int Detail) ParseResponseCode(byte[] data)
    {
        if (data.Length <= ResponseCodeOffset)
            return (ServerErrorClass, 0);

        byte code = data[ResponseCodeOffset];
        return (code >> ResponseClassBits, code & ResponseDetailMask);
    }

    private static void WriteOption(MemoryStream ms, int number, ref int prevNumber, byte[] value)
    {
        int delta = number - prevNumber;
        prevNumber = number;
        int length = value.Length;

        int deltaNibble = delta < ExtendedOneByte ? delta : delta < ExtendedTwoBytes ? ExtendedOneByte : 14;
        int lengthNibble = length < ExtendedOneByte ? length : length < ExtendedTwoBytes ? ExtendedOneByte : 14;

        ms.WriteByte((byte)((deltaNibble << 4) | lengthNibble));

        if (delta >= ExtendedOneByte && delta < ExtendedTwoBytes)
        {
            ms.WriteByte((byte)(delta - ExtendedOneByte));
        }
        else if (delta >= ExtendedTwoBytes)
        {
            Span<byte> b = stackalloc byte[2];
            BinaryPrimitives.WriteUInt16BigEndian(b, (ushort)(delta - ExtendedTwoBytes));
            ms.Write(b);
        }

        if (length >= ExtendedOneByte && length < ExtendedTwoBytes)
        {
            ms.WriteByte((byte)(length - ExtendedOneByte));
        }
        else if (length >= ExtendedTwoBytes)
        {
            Span<byte> b = stackalloc byte[2];
            BinaryPrimitives.WriteUInt16BigEndian(b, (ushort)(length - ExtendedTwoBytes));
            ms.Write(b);
        }

        ms.Write(value);
    }
}
