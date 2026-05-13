using System.Text;

namespace DiagKit.FlashFiles.Tests;

internal static class TestRecordFactory
{
    internal static Stream ToStream(params string[] lines)
    {
        var text = string.Join(Environment.NewLine, lines) + Environment.NewLine;
        return new MemoryStream(Encoding.UTF8.GetBytes(text));
    }

    internal static byte[] ToBytes(params string[] lines)
    {
        var text = string.Join(Environment.NewLine, lines) + Environment.NewLine;
        return Encoding.UTF8.GetBytes(text);
    }

    internal static string IntelExtendedLinearAddress(ushort highAddress)
    {
        return IntelRecord(4, 0, [(byte)(highAddress >> 8), (byte)highAddress]);
    }

    internal static string IntelExtendedSegmentAddress(ushort segmentAddress)
    {
        return IntelRecord(2, 0, [(byte)(segmentAddress >> 8), (byte)segmentAddress]);
    }

    internal static string IntelEndOfFile() => ":00000001FF";

    internal static string IntelRecord(byte recordType, ushort address, ReadOnlySpan<byte> data)
    {
        var bytes = new byte[4 + data.Length];
        bytes[0] = (byte)data.Length;
        bytes[1] = (byte)(address >> 8);
        bytes[2] = (byte)address;
        bytes[3] = recordType;
        data.CopyTo(bytes.AsSpan(4));
        return IntelRecord(bytes);
    }

    internal static string IntelData(ushort address, ReadOnlySpan<byte> data)
    {
        return IntelRecord(0, address, data);
    }

    internal static string SRecord(char type, uint address, ReadOnlySpan<byte> data)
    {
        var addressLength = type switch
        {
            '0' or '1' or '5' or '9' => 2,
            '2' or '6' or '8' => 3,
            '3' or '7' => 4,
            _ => throw new ArgumentOutOfRangeException(nameof(type)),
        };
        var count = addressLength + data.Length + 1;
        var bytes = new byte[count + 1];
        bytes[0] = (byte)count;
        for (var i = 0; i < addressLength; i++)
            bytes[1 + i] = (byte)(address >> ((addressLength - i - 1) * 8));
        data.CopyTo(bytes.AsSpan(1 + addressLength));
        bytes[^1] = MotorolaChecksum(bytes.AsSpan(0, bytes.Length - 1));
        return $"S{type}{Convert.ToHexString(bytes)}";
    }

    private static string IntelRecord(ReadOnlySpan<byte> bytes)
    {
        var checksum = IntelChecksum(bytes);
        return $":{Convert.ToHexString(bytes)}{checksum:X2}";
    }

    private static byte IntelChecksum(ReadOnlySpan<byte> bytes)
    {
        var sum = 0;
        foreach (var b in bytes)
            sum += b;
        return (byte)(0x100 - (sum & 0xFF));
    }

    private static byte MotorolaChecksum(ReadOnlySpan<byte> bytes)
    {
        var sum = 0u;
        foreach (var b in bytes)
            sum += b;
        return (byte)(0xFF - (sum & 0xFF));
    }
}
