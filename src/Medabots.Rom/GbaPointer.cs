using System.Buffers.Binary;

namespace Medabots.Rom;

public static class GbaPointer
{
    public const uint BaseAddress = 0x08000000;

    public static uint ToRomAddress(int fileOffset)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(fileOffset);
        return checked((uint)fileOffset + BaseAddress);
    }

    public static int ToFileOffset(uint romAddress)
    {
        if (romAddress < BaseAddress)
        {
            throw new ArgumentOutOfRangeException(nameof(romAddress), "The address is not a valid GBA ROM address.");
        }

        return checked((int)(romAddress - BaseAddress));
    }

    public static bool TryReadFileOffset(ReadOnlySpan<byte> data, int offset, out int fileOffset)
    {
        ValidateRange(data, offset, sizeof(uint));

        var romAddress = BinaryPrimitives.ReadUInt32LittleEndian(data[offset..]);
        if (romAddress == 0)
        {
            fileOffset = -1;
            return false;
        }

        fileOffset = ToFileOffset(romAddress);
        return true;
    }

    public static void WriteFileOffset(Span<byte> data, int offset, int fileOffset)
    {
        ValidateRange(data, offset, sizeof(uint));
        BinaryPrimitives.WriteUInt32LittleEndian(data[offset..], ToRomAddress(fileOffset));
    }

    private static void ValidateRange(ReadOnlySpan<byte> data, int offset, int length)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfNegative(length);

        if (offset + length > data.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(length), "The requested range exceeds the buffer length.");
        }
    }
}
