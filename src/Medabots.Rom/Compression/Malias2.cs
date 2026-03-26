namespace Medabots.Rom.Compression;

public static unsafe class Malias2
{
    public static byte[]? Decompress(byte[] data, int offset)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentOutOfRangeException.ThrowIfNegative(offset);

        fixed (byte* ptr = &data[offset])
        {
            return Decompress(ptr);
        }
    }

    public static byte[]? Decompress(byte* source)
    {
        byte[]? uncompressedData = null;
        if (*source == 0x4C && *(source + 1) == 0x65)
        {
            var length = *(source + 2) | (*(source + 3) << 8) | (*(source + 4) << 16);
            if (length > 0 && length < 0x100000)
            {
                uncompressedData = new byte[length];
                fixed (byte* destination = &uncompressedData[0])
                {
                    if (!DecompressInternal(source, destination))
                    {
                        uncompressedData = null;
                    }
                }
            }
        }

        return uncompressedData;
    }

    private static bool DecompressInternal(byte* source, byte* target)
    {
        var length = *(source + 2) | (*(source + 3) << 8) | (*(source + 4) << 16);
        source += 6;

        while (length > 0)
        {
            var command = *source++;
            var numCommands = 0;

            while (numCommands < 4 && length > 0)
            {
                switch (command & 3)
                {
                    case 0:
                    {
                        var src = *source++ | (*source++ << 8);
                        var copySrc = target - ((src & 0xFFF) + 5);
                        length -= (src >> 12) + 3;
                        var copyLength = (src >> 12) + 2;
                        while (copyLength-- > -1)
                        {
                            *target++ = *copySrc++;
                        }
                        break;
                    }
                    case 1:
                    {
                        var src = *source++;
                        var copySrc = target - ((src & 0x3) + 1);
                        length -= (src >> 2) + 2;
                        var copyLength = (src >> 2) + 1;
                        while (copyLength-- > -1)
                        {
                            *target++ = *copySrc++;
                        }
                        break;
                    }
                    case 2:
                        *target++ = *source++;
                        length--;
                        break;
                    case 3:
                        *target++ = *source++;
                        *target++ = *source++;
                        *target++ = *source++;
                        length -= 3;
                        break;
                }

                numCommands++;
                command >>= 2;
            }
        }

        return true;
    }
}
